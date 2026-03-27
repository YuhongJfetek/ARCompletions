BEGIN;
CREATE TABLE IF NOT EXISTS "LineEventLogs" (
  "Id" BIGSERIAL PRIMARY KEY,
  "LineUserId" text NOT NULL,
  "EventType" text NOT NULL,
  "MessageType" text NULL,
  "Text" text NULL,
  "RawJson" text NOT NULL,
  "CreatedAt" timestamp without time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_LineEventLogs_LineUserId" ON "LineEventLogs" ("LineUserId");
COMMIT;
