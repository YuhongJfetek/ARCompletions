CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251014055513_InitialCreate') THEN
    CREATE TABLE "Completions" (
        "Id" TEXT NOT NULL,
        "VenuesId" TEXT NOT NULL,
        "UserId" TEXT NOT NULL,
        "Complate" INTEGER NOT NULL,
        "CreatedAt" INTEGER NOT NULL,
        CONSTRAINT "PK_Completions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251014055513_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251014055513_InitialCreate', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260112023007_AddCompletionsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260112023007_AddCompletionsTable', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260112032026___CheckDiff') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260112032026___CheckDiff', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226063817_AddLineEventAndChatMessage') THEN
    CREATE TABLE "ChatMessages" (
        "Id" TEXT NOT NULL,
        "UserId" TEXT NOT NULL,
        "Role" TEXT NOT NULL,
        "Content" TEXT NOT NULL,
        "Meta" TEXT NOT NULL,
        "CreatedAt" INTEGER NOT NULL,
        CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226063817_AddLineEventAndChatMessage') THEN
    CREATE TABLE "LineEvents" (
        "Id" TEXT NOT NULL,
        "EventId" TEXT NOT NULL,
        "UserId" TEXT NOT NULL,
        "EventType" TEXT NOT NULL,
        "RawEvent" TEXT NOT NULL,
        "ReceivedAt" INTEGER NOT NULL,
        "Processed" INTEGER NOT NULL,
        CONSTRAINT "PK_LineEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226063817_AddLineEventAndChatMessage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260226063817_AddLineEventAndChatMessage', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE TABLE "AnalysisDetails" (
        "Id" TEXT NOT NULL,
        "JobId" TEXT NOT NULL,
        "CompletionId" TEXT NOT NULL,
        "Similarity" REAL NOT NULL,
        "RiskLevel" INTEGER NOT NULL,
        "Flags" TEXT NOT NULL,
        CONSTRAINT "PK_AnalysisDetails" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE TABLE "AnalysisJobs" (
        "Id" TEXT NOT NULL,
        "Type" TEXT NOT NULL,
        "Params" TEXT NOT NULL,
        "Status" TEXT NOT NULL,
        "Progress" INTEGER NOT NULL,
        "ResultSummary" TEXT NOT NULL,
        "CreatedAt" INTEGER NOT NULL,
        "StartedAt" INTEGER,
        "FinishedAt" INTEGER,
        "Error" TEXT NOT NULL,
        CONSTRAINT "PK_AnalysisJobs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE TABLE "AuditLogs" (
        "Id" TEXT NOT NULL,
        "Actor" TEXT NOT NULL,
        "Action" TEXT NOT NULL,
        "TargetId" TEXT NOT NULL,
        "Payload" TEXT NOT NULL,
        "Timestamp" INTEGER NOT NULL,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE INDEX "IX_LineEvents_EventId" ON "LineEvents" ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE INDEX "IX_ChatMessages_UserId" ON "ChatMessages" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    CREATE INDEX "IX_AnalysisJobs_Status" ON "AnalysisJobs" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226064821_AddAuditAndAnalysis') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260226064821_AddAuditAndAnalysis', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226065446_AddChatEmbeddingsAuto') THEN
    CREATE TABLE "ChatEmbeddings" (
        "Id" TEXT NOT NULL,
        "ChatMessageId" TEXT NOT NULL,
        "EmbeddingJson" TEXT NOT NULL,
        "CreatedAt" INTEGER NOT NULL,
        CONSTRAINT "PK_ChatEmbeddings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260226065446_AddChatEmbeddingsAuto') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260226065446_AddChatEmbeddingsAuto', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE TABLE "Feedbacks" (
        "Id" TEXT NOT NULL,
        "UserId" TEXT,
        "Rating" INTEGER NOT NULL,
        "Comment" TEXT,
        "CreatedAt" INTEGER NOT NULL,
        CONSTRAINT "PK_Feedbacks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE TABLE "LineEventLogs" (
        "Id" INTEGER NOT NULL,
        "LineUserId" TEXT NOT NULL,
        "EventType" TEXT NOT NULL,
        "MessageType" TEXT,
        "Text" TEXT,
        "RawJson" TEXT NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        CONSTRAINT "PK_LineEventLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE TABLE "LineUsers" (
        "Id" INTEGER NOT NULL,
        "LineUserId" TEXT NOT NULL,
        "DisplayName" TEXT,
        "PictureUrl" TEXT,
        "CreatedAt" TEXT NOT NULL,
        "LastSeenAt" TEXT NOT NULL,
        "IsFollowed" INTEGER NOT NULL,
        CONSTRAINT "PK_LineUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE TABLE "UnmatchedQueries" (
        "Id" TEXT NOT NULL,
        "Query" TEXT NOT NULL,
        "Source" TEXT NOT NULL,
        "CreatedAt" INTEGER NOT NULL,
        CONSTRAINT "PK_UnmatchedQueries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE INDEX "IX_Feedbacks_CreatedAt" ON "Feedbacks" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE INDEX "IX_LineEventLogs_LineUserId" ON "LineEventLogs" ("LineUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE UNIQUE INDEX "IX_LineUsers_LineUserId" ON "LineUsers" ("LineUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    CREATE INDEX "IX_UnmatchedQueries_CreatedAt" ON "UnmatchedQueries" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309031037_AddLineEventLogs') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260309031037_AddLineEventLogs', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "UnmatchedQueries" ALTER COLUMN "Source" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "UnmatchedQueries" ALTER COLUMN "Query" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "UnmatchedQueries" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "UnmatchedQueries" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "PictureUrl" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "LineUserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "LastSeenAt" TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "IsFollowed" TYPE boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "DisplayName" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "CreatedAt" TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineUsers" ALTER COLUMN "Id" TYPE bigint;
    ALTER TABLE "LineUsers" ALTER COLUMN "Id" DROP DEFAULT;
    ALTER TABLE "LineUsers" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "UserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "ReceivedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "RawEvent" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "Processed" TYPE boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "EventType" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "EventId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEvents" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "Text" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "RawJson" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "MessageType" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "LineUserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "EventType" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "CreatedAt" TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "LineEventLogs" ALTER COLUMN "Id" TYPE bigint;
    ALTER TABLE "LineEventLogs" ALTER COLUMN "Id" DROP DEFAULT;
    ALTER TABLE "LineEventLogs" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Feedbacks" ALTER COLUMN "UserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Feedbacks" ALTER COLUMN "Rating" TYPE integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Feedbacks" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Feedbacks" ALTER COLUMN "Comment" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Feedbacks" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Completions" ALTER COLUMN "VenuesId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Completions" ALTER COLUMN "UserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Completions" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Completions" ALTER COLUMN "Complate" TYPE boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "Completions" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "UserId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "Role" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "Meta" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "Content" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatMessages" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatEmbeddings" ALTER COLUMN "EmbeddingJson" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatEmbeddings" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatEmbeddings" ALTER COLUMN "ChatMessageId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "ChatEmbeddings" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "Timestamp" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "TargetId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "Payload" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "Actor" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "Action" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AuditLogs" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Type" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Status" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "StartedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "ResultSummary" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Progress" TYPE integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Params" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "FinishedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Error" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "CreatedAt" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisJobs" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "Similarity" TYPE double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "RiskLevel" TYPE integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "JobId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "Flags" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "CompletionId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    ALTER TABLE "AnalysisDetails" ALTER COLUMN "Id" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260309070058_SyncModel') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260309070058_SyncModel', '9.0.10');
    END IF;
END $EF$;
COMMIT;

