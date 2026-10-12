const { createClient } = require('@supabase/supabase-js');

function createUserClient(token) {
    const url = process.env.SUPABASE_URL;
    const key = process.env.SUPABASE_ANON_KEY;
    if (!url || !key) {
        throw new Error('Supabase settings are missing');
    }

    // Each request uses its own token so users do not share a login session.
    return createClient(url, key, {
        auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false },
        global: { headers: { Authorization: `Bearer ${token}` } }
    });
}

module.exports = { createUserClient };
