const path = require('node:path');
require('dotenv').config({ path: path.join(__dirname, '.env'), quiet: true });

const { createApp } = require('./app');
const { createUserClient } = require('./supabase');

try {
    const app = createApp({
        authRoutes: require('./auth'),
        createUserClient
    });
    const port = process.env.PORT || 3000;
    app.listen(port, () => console.log(`Server is running on port ${port}`));
} catch (error) {
    console.error('Backend could not start. Check BackEnd/.env and the setup instructions.');
    process.exitCode = 1;
}
