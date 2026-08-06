using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260801120000_GameListCollaboration")]
    public partial class GameListCollaboration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF OBJECT_ID(N'[dbo].[GameListCollaborators]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[GameListCollaborators]
                (
                    [GameListId] int NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [InvitedByUserId] uniqueidentifier NOT NULL,
                    [Status] int NOT NULL,
                    [InvitedAtUtc] datetime2 NOT NULL,
                    [RespondedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_GameListCollaborators] PRIMARY KEY ([GameListId], [UserId]),
                    CONSTRAINT [FK_GameListCollaborators_GameLists_GameListId]
                        FOREIGN KEY ([GameListId]) REFERENCES [dbo].[GameLists] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_GameListCollaborators_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_GameListCollaborators_AspNetUsers_InvitedByUserId]
                        FOREIGN KEY ([InvitedByUserId]) REFERENCES [dbo].[AspNetUsers] ([Id])
                );

                CREATE INDEX [IX_GameListCollaborators_InvitedByUserId]
                    ON [dbo].[GameListCollaborators] ([InvitedByUserId]);

                CREATE INDEX [IX_GameListCollaborators_UserId_Status_InvitedAtUtc]
                    ON [dbo].[GameListCollaborators] ([UserId], [Status], [InvitedAtUtc]);
            END
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF OBJECT_ID(N'[dbo].[GameListCollaborators]', N'U') IS NOT NULL
                DROP TABLE [dbo].[GameListCollaborators];
            """);
        }
    }
}
