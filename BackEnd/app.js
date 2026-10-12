const express = require('express');
const { createLessonRoutes } = require('./lessons');

function createApp({ authRoutes, createUserClient }) {
    const app = express();
    app.use(express.json({ limit: '32kb' }));
    app.get('/', (req, res) => res.send('BCSignVR Backend is running'));
    app.use('/api/auth', authRoutes);
    app.use('/api', createLessonRoutes(createUserClient));

    app.use((req, res) => res.status(404).json({ error: 'Route not found' }));
    app.use((error, req, res, next) => {
        if (error.type === 'entity.parse.failed') {
            return res.status(400).json({ error: 'Request body must be valid JSON' });
        }
        if (error.type === 'entity.too.large') {
            return res.status(413).json({ error: 'Request body is too large' });
        }
        res.status(500).json({ error: 'Could not complete the request' });
    });
    return app;
}

module.exports = { createApp };
