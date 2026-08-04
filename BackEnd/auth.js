const express = require('express');
const router = express.Router();

router.post('/register', (req, res) => {
    res.send('Register endpoint is working');
});

router.post('/login', (req, res) => {
    res.send('Login endpoint is working');
});

module.exports = router;