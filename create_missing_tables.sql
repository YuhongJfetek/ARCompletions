BEGIN;

CREATE TABLE IF NOT EXISTS "LineUsers" (
  "Id" BIGSERIAL PRIMARY KEY,
  "LineUserId" text NOT NULL,
  "DisplayName" text,
  "PictureUrl" text,
  "CreatedAt" timestamp without time zone NOT NULL,
  "LastSeenAt" timestamp without time zone NOT NULL,
  "IsFollowed" boolean NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_LineUsers_LineUserId" ON "LineUsers" ("LineUserId");

CREATE TABLE IF NOT EXISTS "UnmatchedQueries" (
  "Id" text PRIMARY KEY,
  "Query" text NOT NULL,
  "Source" text NOT NULL,
  "CreatedAt" bigint NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_UnmatchedQueries_CreatedAt" ON "UnmatchedQueries" ("CreatedAt");

CREATE TABLE IF NOT EXISTS "Feedbacks" (
  "Id" text PRIMARY KEY,
  "UserId" text,
  "Rating" integer NOT NULL,
  "Comment" text,
  "CreatedAt" bigint NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Feedbacks_CreatedAt" ON "Feedbacks" ("CreatedAt");

COMMIT;
