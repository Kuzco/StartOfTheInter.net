namespace Terminal.Domain.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using Terminal.Domain.Entities;
    using System.Collections.Generic;

    public sealed class Configuration : DbMigrationsConfiguration<Terminal.Domain.Entities.EntityContainer>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(Terminal.Domain.Entities.EntityContainer context)
        {
            context.Variables.AddOrUpdate(
                new Variable { Name = "Registration", Value = "Open" }
            );

            context.Users.AddOrUpdate(
                new User
                {
                    Username = "Admin",
                    Password = "12345",
                    Credits = 1000000,
                    JoinDate = DateTime.Now,
                    LastLogin = DateTime.Now,
                    Sound = true,
                    Roles = new List<Role>
                    {
                        new Role { Name = "Administrator" }
                    },
                    Aliases = new List<Alias>
                    {
                        new Alias { Shortcut = "lb", Command = "BOARDS" },
                        new Alias { Shortcut = "b", Command = "BOARD" },
                        new Alias { Shortcut = "t", Command = "TOPIC" },
                        new Alias { Shortcut = "lm", Command = "MESSAGES" },
                        new Alias { Shortcut = "m", Command = "MESSAGE" },
                        new Alias { Shortcut = "ll", Command = "LINKS" },
                        new Alias { Shortcut = "l", Command = "LINK" }
                    }
                }
            );

            context.Roles.AddOrUpdate(
                new Role { Name = "Moderator" },
                new Role { Name = "User" }
            );

            context.Boards.AddOrUpdate(
                new Board
                {
                    BoardID = -1,
                    Name = "Negative",
                    Description = "Only negative discussion is allowed. Express your inner pessimist.",
                    Hidden = true,
                    AllTopics = true
                },
                new Board
                {
                    BoardID = 0,
                    Name = "All Topics",
                    Description = "Read-Only board that shows all topics from all boards.",
                    Locked = true,
                    AllTopics = true
                },
                new Board
                {
                    BoardID = 1,
                    Name = "General",
                    Description = "Discussion of anything and everything since time began.",
                    AllTopics = true
                },
                new Board
                {
                    BoardID = 10,
                    Name = "Moderators",
                    Description = "This board is for moderators only.",
                    ModsOnly = true
                },
                new Board
                {
                    BoardID = 69,
                    Name = "NSFW / NLS",
                    Description = "The back-alley of the internet."
                },
                new Board
                {
                    BoardID = 4,
                    Name = "Anonymous",
                    Description = "An anonymous board where nobody has a face.",
                    Anonymous = true,
                    AllTopics = true
                }
            );
        }
    }
}
