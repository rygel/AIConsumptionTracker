-- Add state column to provider_history.
-- Stores the ProviderUsageState enum value (Available=0, Missing=1, Error=2,
-- ConsoleCheck=3, Unknown=4, Unavailable=5) so the full state is round-tripped
-- through the database and not lost on read-back.
-- DEFAULT 0 = Available, matching the ProviderUsage model default.
ALTER TABLE provider_history ADD COLUMN state INTEGER NOT NULL DEFAULT 0;
