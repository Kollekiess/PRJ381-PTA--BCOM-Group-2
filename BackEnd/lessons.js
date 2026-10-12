const express = require('express');
const { randomUUID } = require('node:crypto');
const store = require('./lesson-store');

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function gestureName(value) {
    const letter = /^(?:Letters?_)?([a-z])$/i.exec(value);
    return letter ? letter[1].toUpperCase() : value;
}

function lessonResponse(lesson) {
    return {
        id: lesson.id,
        title: lesson.title,
        category: lesson.category,
        difficulty: lesson.difficulty,
        targetGestures: (lesson.target_gestures || []).map(gestureName),
        createdAt: lesson.created_at
    };
}

function progressResponse(lessonId, progress) {
    return {
        lessonId,
        completed: progress?.completed || false,
        highestScore: progress?.highest_score || 0,
        lastAttempted: progress?.last_attempted || null
    };
}

function validText(value, max) {
    return typeof value === 'string' && value.trim().length > 0 && value.length <= max;
}

function validTimestamp(value) {
    if (typeof value !== 'string' ||
        !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?(?:Z|[+-]\d{2}:\d{2})$/.test(value)) return false;
    const wallTime = Date.parse(value.slice(0, 19) + 'Z');
    return Number.isFinite(Date.parse(value)) && Number.isFinite(wallTime) &&
        new Date(wallTime).toISOString().slice(0, 19) === value.slice(0, 19);
}

function feedbackError(body) {
    if (!body || typeof body !== 'object' || Array.isArray(body)) return 'Feedback must be a JSON object';
    if ('userId' in body || 'user_id' in body) return 'The user comes from the login token';
    if (typeof body.lessonId !== 'string' || !UUID.test(body.lessonId)) return 'lessonId must be a UUID';
    if (body.attemptId !== undefined && (typeof body.attemptId !== 'string' || !UUID.test(body.attemptId))) return 'attemptId must be a UUID';
    if (!validText(body.target, 80) || !validText(body.predicted, 80)) return 'target and predicted are required';
    for (const field of ['confidence', 'target_score']) {
        if (typeof body[field] !== 'number' || !Number.isFinite(body[field]) || body[field] < 0 || body[field] > 1) {
            return `${field} must be between 0 and 1`;
        }
    }
    if (typeof body.correct !== 'boolean') return 'correct must be true or false';
    if (body.correct && gestureName(body.target) !== gestureName(body.predicted)) return 'A correct result must match the target';
    if (body.target_score > body.confidence + 0.000001 ||
        (gestureName(body.target) === gestureName(body.predicted) && Math.abs(body.target_score - body.confidence) > 0.000001)) {
        return 'The scores do not match the predicted gesture';
    }
    for (const field of ['message', 'hint']) {
        if (body[field] !== undefined && (typeof body[field] !== 'string' || body[field].length > 300)) {
            return `${field} must be text with at most 300 characters`;
        }
    }
    if (!validTimestamp(body.attemptedAt)) {
        return 'attemptedAt must be an ISO timestamp with a timezone';
    }
    return null;
}

function createLessonRoutes(createUserClient) {
    const router = express.Router();

    router.use(async (req, res, next) => {
        const match = /^Bearer ([^\s]+)$/i.exec(req.get('authorization') || '');
        if (!match) return res.status(401).json({ error: 'A login token is required' });
        try {
            req.db = createUserClient(match[1]);
        } catch (error) {
            return res.status(503).json({ error: 'Supabase is not configured' });
        }
        try {
            const { data, error } = await req.db.auth.getUser(match[1]);
            if (error) {
                const status = error.status >= 500 || error.name === 'AuthRetryableFetchError' ? 503 : 401;
                return res.status(status).json({ error: status === 503 ? 'Login service is unavailable' : 'Login token is invalid or expired' });
            }
            if (!data?.user) return res.status(401).json({ error: 'Login token is invalid or expired' });
            req.userId = data.user.id;
            next();
        } catch (error) {
            res.status(503).json({ error: 'Login service is unavailable' });
        }
    });

    router.param('lessonId', (req, res, next, id) => {
        if (!UUID.test(id)) return res.status(400).json({ error: 'lessonId must be a UUID' });
        next();
    });

    router.get('/lessons', async (req, res) => {
        try {
            res.json({ lessons: (await store.getLessons(req.db)).map(lessonResponse) });
        } catch (error) {
            res.status(503).json({ error: 'Could not load lessons' });
        }
    });

    router.get('/lessons/:lessonId', async (req, res) => {
        try {
            const lesson = await store.getLesson(req.db, req.params.lessonId);
            if (!lesson) return res.status(404).json({ error: 'Lesson not found' });
            res.json(lessonResponse(lesson));
        } catch (error) {
            res.status(503).json({ error: 'Could not load lesson' });
        }
    });

    router.get('/lessons/:lessonId/progress', async (req, res) => {
        try {
            const id = req.params.lessonId;
            if (!await store.getLesson(req.db, id)) return res.status(404).json({ error: 'Lesson not found' });
            res.json(progressResponse(id, await store.getProgress(req.db, req.userId, id)));
        } catch (error) {
            res.status(503).json({ error: 'Could not load progress' });
        }
    });

    router.post('/feedback', async (req, res) => {
        const invalid = feedbackError(req.body);
        if (invalid) return res.status(400).json({ error: invalid });
        const body = req.body;
        try {
            const lesson = await store.getLesson(req.db, body.lessonId);
            if (!lesson) return res.status(404).json({ error: 'Lesson not found' });
            const target = gestureName(body.target);
            if (!(lesson.target_gestures || []).map(gestureName).includes(target)) {
                return res.status(400).json({ error: 'The target gesture is not part of this lesson' });
            }
            const saved = await store.saveAttempt(req.db, {
                id: body.attemptId || randomUUID(),
                lessonId: body.lessonId,
                gestureId: target,
                score: body.target_score * 100,
                attemptedAt: new Date(body.attemptedAt).toISOString()
            });
            res.status(saved.duplicate ? 200 : 201).json({
                attemptId: saved.attemptId,
                gestureId: target,
                predicted: gestureName(body.predicted),
                confidence: body.confidence,
                target_score: body.target_score,
                correct: body.correct,
                feedbackMessage: body.message ?? (body.correct ? 'Correct!' : `Try ${target} again.`),
                hint: body.hint ?? '',
                updatedProgress: progressResponse(body.lessonId, saved.progress)
            });
        } catch (error) {
            const status = error.code === '22023' ? 409 : 503;
            res.status(status).json({ error: status === 409 ? 'attemptId was already used for a different result' : 'Could not save feedback' });
        }
    });

    return router;
}

module.exports = { createLessonRoutes };
