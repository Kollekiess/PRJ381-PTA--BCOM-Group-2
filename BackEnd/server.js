const express = require('express');
const app = express();
const authRoutes = require('./auth');
const PORT = 3000;

app.use(express.json());

app.get('/', (req, res) => {
    res.send('BCSignVR Backend is running');
});

app.listen(PORT, () => {
    console.log(`Server is running on http://localhost:${PORT}`);
});