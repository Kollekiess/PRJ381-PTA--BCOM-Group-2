from database.supabase import supabase

print("Connected")
print(supabase)

response = (
    supabase.table("signs")
   .insert({
        "sign_name": "A",
        "accuracy": 95.2,
        "handedness": "Right"
    })
    .execute()
)

savedsigns = (
    supabase.table("signs")
    .select("*")
    .limit(5)
    .execute()
)

print(response)

print(savedsigns.data)


