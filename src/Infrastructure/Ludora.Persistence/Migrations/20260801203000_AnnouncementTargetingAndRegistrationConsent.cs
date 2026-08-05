using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260801203000_AnnouncementTargetingAndRegistrationConsent")]
public partial class AnnouncementTargetingAndRegistrationConsent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Announcements', 'Audience') IS NULL
            BEGIN
                ALTER TABLE dbo.Announcements
                ADD Audience int NOT NULL
                    CONSTRAINT DF_Announcements_Audience DEFAULT(0);
            END;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE name = 'CK_Announcements_Audience'
                  AND parent_object_id = OBJECT_ID('dbo.Announcements'))
            BEGIN
                ALTER TABLE dbo.Announcements WITH CHECK
                ADD CONSTRAINT CK_Announcements_Audience
                CHECK ([Audience] BETWEEN 0 AND 7);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_Announcements_IsPublished_Audience_CreatedAt'
                  AND object_id = OBJECT_ID('dbo.Announcements'))
            BEGIN
                CREATE INDEX IX_Announcements_IsPublished_Audience_CreatedAt
                ON dbo.Announcements(IsPublished, Audience, CreatedAt);
            END;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.AnnouncementViews', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AnnouncementViews
                (
                    Id bigint IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_AnnouncementViews PRIMARY KEY,
                    AnnouncementId int NOT NULL,
                    UserId uniqueidentifier NOT NULL,
                    FirstViewedAtUtc datetime2 NOT NULL,
                    CONSTRAINT FK_AnnouncementViews_Announcements_AnnouncementId
                        FOREIGN KEY (AnnouncementId)
                        REFERENCES dbo.Announcements(Id)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_AnnouncementViews_AspNetUsers_UserId
                        FOREIGN KEY (UserId)
                        REFERENCES dbo.AspNetUsers(Id)
                        ON DELETE CASCADE
                );
            END;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_AnnouncementViews_AnnouncementId_UserId'
                  AND object_id = OBJECT_ID('dbo.AnnouncementViews'))
            BEGIN
                CREATE UNIQUE INDEX IX_AnnouncementViews_AnnouncementId_UserId
                ON dbo.AnnouncementViews(AnnouncementId, UserId);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_AnnouncementViews_AnnouncementId_FirstViewedAtUtc'
                  AND object_id = OBJECT_ID('dbo.AnnouncementViews'))
            BEGIN
                CREATE INDEX IX_AnnouncementViews_AnnouncementId_FirstViewedAtUtc
                ON dbo.AnnouncementViews(AnnouncementId, FirstViewedAtUtc);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_AnnouncementViews_UserId'
                  AND object_id = OBJECT_ID('dbo.AnnouncementViews'))
            BEGIN
                CREATE INDEX IX_AnnouncementViews_UserId
                ON dbo.AnnouncementViews(UserId);
            END;
            """);

        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.AspNetUsers', 'TermsAcceptedAtUtc') IS NULL
                ALTER TABLE dbo.AspNetUsers ADD TermsAcceptedAtUtc datetime2 NULL;

            IF COL_LENGTH('dbo.AspNetUsers', 'PrivacyAcceptedAtUtc') IS NULL
                ALTER TABLE dbo.AspNetUsers ADD PrivacyAcceptedAtUtc datetime2 NULL;

            IF COL_LENGTH('dbo.AspNetUsers', 'TermsVersion') IS NULL
                ALTER TABLE dbo.AspNetUsers ADD TermsVersion nvarchar(32) NULL;

            IF COL_LENGTH('dbo.AspNetUsers', 'PrivacyVersion') IS NULL
                ALTER TABLE dbo.AspNetUsers ADD PrivacyVersion nvarchar(32) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.AnnouncementViews', N'U') IS NOT NULL
                DROP TABLE dbo.AnnouncementViews;

            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_Announcements_Audience')
                ALTER TABLE dbo.Announcements DROP CONSTRAINT CK_Announcements_Audience;

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_Announcements_IsPublished_Audience_CreatedAt'
                  AND object_id = OBJECT_ID('dbo.Announcements'))
                DROP INDEX IX_Announcements_IsPublished_Audience_CreatedAt
                ON dbo.Announcements;

            IF COL_LENGTH('dbo.Announcements', 'Audience') IS NOT NULL
            BEGIN
                DECLARE @AudienceDefaultConstraint sysname;
                SELECT @AudienceDefaultConstraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.Announcements')
                  AND c.name = 'Audience';

                IF @AudienceDefaultConstraint IS NOT NULL
                    EXEC('ALTER TABLE dbo.Announcements DROP CONSTRAINT [' + @AudienceDefaultConstraint + ']');

                ALTER TABLE dbo.Announcements DROP COLUMN Audience;
            END;

            IF COL_LENGTH('dbo.AspNetUsers', 'TermsAcceptedAtUtc') IS NOT NULL
                ALTER TABLE dbo.AspNetUsers DROP COLUMN TermsAcceptedAtUtc;

            IF COL_LENGTH('dbo.AspNetUsers', 'PrivacyAcceptedAtUtc') IS NOT NULL
                ALTER TABLE dbo.AspNetUsers DROP COLUMN PrivacyAcceptedAtUtc;

            IF COL_LENGTH('dbo.AspNetUsers', 'TermsVersion') IS NOT NULL
                ALTER TABLE dbo.AspNetUsers DROP COLUMN TermsVersion;

            IF COL_LENGTH('dbo.AspNetUsers', 'PrivacyVersion') IS NOT NULL
                ALTER TABLE dbo.AspNetUsers DROP COLUMN PrivacyVersion;
            """);
    }
}
