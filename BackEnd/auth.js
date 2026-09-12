const express = require('express');
const router = express.Router();
require('dotenv').config(); // load env keys so the app doesn't crash

const { createClient } = require('@supabase/supabase-js');

// hook up Tiaan's db connection
const supabase = createClient(process.env.SUPABASE_URL, process.env.SUPABASE_ANON_KEY);

// REGISTER
router.post('/register', async (req, res) => {
    try {
        const { email, password } = req.body;

        if (!email || !password) {
            return res.status(400).json({ error: 'need both email and password' });
        }

        // push new user to supabase
        const { data, error } = await supabase.auth.signUp({ email, password });

        if (error) {
            return res.status(400).json({ error: error.message });
        }

        res.status(201).json({ message: 'registered successfully', user: data.user });
    } catch (err) {
        res.status(500).json({ error: 'server error' });
    }
});

// LOGIN
router.post('/login', async (req, res) => {
    try {
        const { email, password } = req.body;

        if (!email || !password) {
            return res.status(400).json({ error: 'need both email and password' });
        }

        // check creds against db
        const { data, error } = await supabase.auth.signInWithPassword({ email, password });

        if (error) {
            return res.status(400).json({ error: 'invalid credentials' });
        }

        res.status(200).json({ message: 'login success', session: data.session });
    } catch (err) {
        res.status(500).json({ error: 'server error' });
    }
});

module.exports = router;