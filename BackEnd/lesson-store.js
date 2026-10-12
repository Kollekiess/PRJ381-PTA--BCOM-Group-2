const LESSON_FIELDS = 'id,title,category,difficulty,target_gestures,created_at';
const PROGRESS_FIELDS = 'lesson_id,completed,highest_score,last_attempted';

async function getLessons(client) {
    const { data, error } = await client.from('lessons')
        .select(LESSON_FIELDS).order('difficulty').order('title');
    if (error) throw error;
    return data;
}

async function getLesson(client, id) {
    const { data, error } = await client.from('lessons')
        .select(LESSON_FIELDS).eq('id', id).maybeSingle();
    if (error) throw error;
    return data;
}

async function getProgress(client, userId, lessonId) {
    const { data, error } = await client.from('user_lessons')
        .select(PROGRESS_FIELDS).eq('user_id', userId).eq('lesson_id', lessonId).maybeSingle();
    if (error) throw error;
    return data;
}

async function saveAttempt(client, attempt) {
    // One database transaction saves the attempt and updates its best score.
    const { data, error } = await client.rpc('record_lesson_feedback', {
        p_attempt_id: attempt.id,
        p_lesson_id: attempt.lessonId,
        p_gesture_id: attempt.gestureId,
        p_score: attempt.score,
        p_attempted_at: attempt.attemptedAt
    });
    if (error) throw error;
    return data;
}

module.exports = { getLessons, getLesson, getProgress, saveAttempt };
