using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260801170000_CommunityRulesFlairsEvents")]
public partial class CommunityRulesFlairsEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[ForumCommunityRules]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ForumCommunityRules]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [CommunityId] int NOT NULL,
                    [Title] nvarchar(100) NOT NULL,
                    [Description] nvarchar(500) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ForumCommunityRules_IsDeleted] DEFAULT 0,
                    CONSTRAINT [PK_ForumCommunityRules] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForumCommunityRules_ForumCommunities_CommunityId]
                        FOREIGN KEY ([CommunityId]) REFERENCES [dbo].[ForumCommunities] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_ForumCommunityRules_CommunityId_SortOrder]
                    ON [dbo].[ForumCommunityRules] ([CommunityId], [SortOrder]);
            END;

            IF OBJECT_ID(N'[dbo].[ForumCommunityFlairs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ForumCommunityFlairs]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [CommunityId] int NOT NULL,
                    [Name] nvarchar(40) NOT NULL,
                    [BackgroundColor] nvarchar(7) NOT NULL,
                    [TextColor] nvarchar(7) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_ForumCommunityFlairs_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ForumCommunityFlairs_IsDeleted] DEFAULT 0,
                    CONSTRAINT [PK_ForumCommunityFlairs] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForumCommunityFlairs_ForumCommunities_CommunityId]
                        FOREIGN KEY ([CommunityId]) REFERENCES [dbo].[ForumCommunities] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_ForumCommunityFlairs_CommunityId_Name]
                    ON [dbo].[ForumCommunityFlairs] ([CommunityId], [Name]);
                CREATE INDEX [IX_ForumCommunityFlairs_CommunityId_SortOrder]
                    ON [dbo].[ForumCommunityFlairs] ([CommunityId], [SortOrder]);
            END;

            IF COL_LENGTH('dbo.ForumCommunityMembers', 'FlairId') IS NULL
                ALTER TABLE [dbo].[ForumCommunityMembers] ADD [FlairId] int NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ForumCommunityMembers_FlairId' AND object_id = OBJECT_ID('dbo.ForumCommunityMembers'))
                CREATE INDEX [IX_ForumCommunityMembers_FlairId] ON [dbo].[ForumCommunityMembers] ([FlairId]);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ForumCommunityMembers_ForumCommunityFlairs_FlairId')
                ALTER TABLE [dbo].[ForumCommunityMembers]
                    ADD CONSTRAINT [FK_ForumCommunityMembers_ForumCommunityFlairs_FlairId]
                    FOREIGN KEY ([FlairId]) REFERENCES [dbo].[ForumCommunityFlairs] ([Id]);

            IF OBJECT_ID(N'[dbo].[ForumCommunityEvents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ForumCommunityEvents]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [CommunityId] int NOT NULL,
                    [CreatedByUserId] uniqueidentifier NOT NULL,
                    [Title] nvarchar(120) NOT NULL,
                    [Description] nvarchar(1500) NOT NULL,
                    [Type] int NOT NULL,
                    [GameName] nvarchar(120) NULL,
                    [Platform] nvarchar(80) NULL,
                    [JoinUrl] nvarchar(2048) NULL,
                    [StartsAtUtc] datetime2 NOT NULL,
                    [EndsAtUtc] datetime2 NULL,
                    [MaxParticipants] int NULL,
                    [IsCancelled] bit NOT NULL CONSTRAINT [DF_ForumCommunityEvents_IsCancelled] DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ForumCommunityEvents_IsDeleted] DEFAULT 0,
                    CONSTRAINT [PK_ForumCommunityEvents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForumCommunityEvents_ForumCommunities_CommunityId]
                        FOREIGN KEY ([CommunityId]) REFERENCES [dbo].[ForumCommunities] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ForumCommunityEvents_AspNetUsers_CreatedByUserId]
                        FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[AspNetUsers] ([Id])
                );
                CREATE INDEX [IX_ForumCommunityEvents_CommunityId_StartsAtUtc]
                    ON [dbo].[ForumCommunityEvents] ([CommunityId], [StartsAtUtc]);
                CREATE INDEX [IX_ForumCommunityEvents_CreatedByUserId_StartsAtUtc]
                    ON [dbo].[ForumCommunityEvents] ([CreatedByUserId], [StartsAtUtc]);
            END;

            IF OBJECT_ID(N'[dbo].[ForumCommunityEventParticipants]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ForumCommunityEventParticipants]
                (
                    [EventId] int NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [JoinedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ForumCommunityEventParticipants] PRIMARY KEY ([EventId], [UserId]),
                    CONSTRAINT [FK_ForumCommunityEventParticipants_ForumCommunityEvents_EventId]
                        FOREIGN KEY ([EventId]) REFERENCES [dbo].[ForumCommunityEvents] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ForumCommunityEventParticipants_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id])
                );
                CREATE INDEX [IX_ForumCommunityEventParticipants_UserId_JoinedAtUtc]
                    ON [dbo].[ForumCommunityEventParticipants] ([UserId], [JoinedAtUtc]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[ForumCommunityEventParticipants]', N'U') IS NOT NULL
                DROP TABLE [dbo].[ForumCommunityEventParticipants];
            IF OBJECT_ID(N'[dbo].[ForumCommunityEvents]', N'U') IS NOT NULL
                DROP TABLE [dbo].[ForumCommunityEvents];
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ForumCommunityMembers_ForumCommunityFlairs_FlairId')
                ALTER TABLE [dbo].[ForumCommunityMembers] DROP CONSTRAINT [FK_ForumCommunityMembers_ForumCommunityFlairs_FlairId];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ForumCommunityMembers_FlairId' AND object_id = OBJECT_ID('dbo.ForumCommunityMembers'))
                DROP INDEX [IX_ForumCommunityMembers_FlairId] ON [dbo].[ForumCommunityMembers];
            IF COL_LENGTH('dbo.ForumCommunityMembers', 'FlairId') IS NOT NULL
                ALTER TABLE [dbo].[ForumCommunityMembers] DROP COLUMN [FlairId];
            IF OBJECT_ID(N'[dbo].[ForumCommunityFlairs]', N'U') IS NOT NULL
                DROP TABLE [dbo].[ForumCommunityFlairs];
            IF OBJECT_ID(N'[dbo].[ForumCommunityRules]', N'U') IS NOT NULL
                DROP TABLE [dbo].[ForumCommunityRules];
            """);
    }
}
