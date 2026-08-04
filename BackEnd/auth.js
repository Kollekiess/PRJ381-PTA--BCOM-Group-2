const express = require('express');
const router = express.Router();

const users = [];

router.post('/register', async (req, res) => {
    try {
        const { email, password } = req.body;

        if (!email || !password) {
            return res.status(400).json({ error: 'Email and password are required' });
        }

        const existingUser = users.find(u => u.email === email);
        if (existingUser) {
            return res.status(400).json({ error: 'User already exists' });
        }

        users.push({ email, password });

        res.status(201).json({ message: 'User registered successfully!' });
    } catch (err) {
        res.status(500).json({ error: 'Server error during registration' });
    }
});

router.post('/login', (req, res) => {
    res.json({ message: 'Login endpoint coming up next!' });
});

module.exports = router;