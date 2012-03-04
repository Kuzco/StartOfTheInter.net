﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Terminal.Domain.Entities;
using Terminal.Domain.Objects;
using Terminal.Domain.Utilities;
using Terminal.Domain.Enums;
using Terminal.Domain.Commands.Interfaces;
using Terminal.Domain.Commands.Objects;
using Terminal.Domain.ExtensionMethods;
using System.IO;
using Mono.Options;
using Terminal.Domain.Repositories.Interfaces;
using System.Web;
using Terminal.Domain.Settings;
using CodeKicker.BBCode;

namespace Terminal.Domain
{
    /// <summary>
    /// The terminal core is the entry point to Terminal.Domain.
    /// Pass in a command string adn Terminal.Domain will parse it and execute it.
    /// Set a command context first and Terminal.Domain will handle the command context as well.
    /// Set a user and Terminal.Domain will automatically determine which commands are avialable based on the user's roles.
    /// </summary>
    public class TerminalCore
    {
        #region Fields & Properties

        private List<ICommand> _commands;
        private IUserRepository _userRepository;
        private IAliasRepository _aliasRepository;
        private IMessageRepository _messageRepository;

        /// <summary>
        /// The current user. Use this for desktop applications where the User object can stay in memory the whole time.
        /// </summary>
        private User _currentUser;

        private CommandContext _commandContext;

        /// <summary>
        /// If set to true then Terminal.Domain will automatically parse the display results for HTML viewing.
        /// Line-breaks will be turned into <br /> tags.
        /// BBCode tags will be parsed and turned into their HTML equivalents.
        /// </summary>
        public bool ParseAsHtml { get; set; }

        /// <summary>
        /// The current username. This is an alternative to setting CurrentUser.
        /// Setting the username will cause the terminal core to pull the user from the database based on their username.
        /// This is ideal for web applications where a User object cannot be held in memory the entire time and must be retrieved on each request.
        /// </summary>
        public string Username
        {
            get
            {
                return _currentUser != null ? _currentUser.Username : "Visitor";
            }
            set
            {
                _currentUser = value != null ? _userRepository.GetUser(value) : null;
            }
        }

        public string IPAddress
        {
            get
            {
                return _currentUser != null ? _currentUser.IPAddress : _ipAddress;
            }
            set
            {
                if (_currentUser != null)
                {
                    _currentUser.IPAddress = value;
                    _userRepository.UpdateUser(_currentUser);
                }
                _ipAddress = value;
            }
        }
        private string _ipAddress;

        /// <summary>
        /// The command context tells the terminal core what state the application is in.
        /// It must be set before calling the ExecuteCommand method and it is returned as part of the command result returned by ExecuteCommand.
        /// 
        /// Options:
        /// 
        /// Disabled - normal command execution will occur.
        /// Passive - normal command execution will occur. If the command is unrecognized then it attempts to use the command context and appends the current command string as additional arguments to the contexted command.
        /// Forced - The contexted command will be used no matter what and the command string will be appended as additional arguments. If the "CANCEL" command is provided by itself then the context is restored to it's state before it was set to forced.
        /// </summary>
        public CommandContext CommandContext
        {
            get
            {
                return _commandContext;
            }
            set
            {
                _commandContext = value ?? new CommandContext();
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the core. Ideally this should be created by Ninject to ensure all dependencies are handled appropriately.
        /// Note: A TerminalBindings class lives in the Terminal.Domain.Ninject.BindingModules namespace. Use this when building your Ninject kernel to ensure proper dependency injection.
        /// 
        /// Sampel: IKernel kernel = new StandardKernel(new TerminalBindings());
        /// </summary>
        /// <param name="commands">A list of all commands available to the application.</param>
        /// <param name="userRepository">The user repository used to retrieve the current user from the database.</param>
        public TerminalCore(
            List<ICommand> commands,
            IUserRepository userRepository,
            IAliasRepository aliasRepository,
            IMessageRepository messageRepository
        )
        {
            _commands = commands;
            _userRepository = userRepository;
            _aliasRepository = aliasRepository;
            _messageRepository = messageRepository;
            this._commandContext = new CommandContext();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Accepts a command string and handles the parsing and execution of the command and the included arguments.
        /// </summary>
        /// <param name="commandString">The command string. Usually a string passed in from a command line interface by the user.</param>
        /// <returns>A CommandResult option containing properties relevant to how data should be processed by the UI.</returns>
        public CommandResult ExecuteCommand(string commandString)
        {
            if (commandString == null) commandString = string.Empty;
            var hasSpace = commandString.Contains(' ');
            var spaceIndex = 0;
            if (hasSpace)
                spaceIndex = commandString.IndexOf(' ');

            // Parse command string to find the command name.
            string commandName = hasSpace ? commandString.Remove(spaceIndex) : commandString;

            if (_currentUser != null)
            {
                _currentUser.LastLogin = DateTime.UtcNow;
                _userRepository.UpdateUser(_currentUser);

                // Check for alias. Replace command name with alias.
                if ((_commandContext.Status & ContextStatus.Forced) == 0)
                {
                    var alias = _aliasRepository.GetAlias(_currentUser.Username, commandName);
                    if (alias != null)
                    {
                        commandString = hasSpace ? alias.Command + commandString.Remove(0, spaceIndex) : alias.Command;
                        hasSpace = commandString.Contains(' ');
                        spaceIndex = 0;
                        if (hasSpace)
                            spaceIndex = commandString.IndexOf(' ');
                        commandName = hasSpace ? commandString.Remove(spaceIndex) : commandString;
                    }
                }
            }

            // Parse command string and divide up arguments into a string array.
            var args = hasSpace ?
                commandString.Remove(0, spaceIndex)
                .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : null;

            // Obtain all roles the current user is a part of.
            var availableRoles = _currentUser != null ? _currentUser.Roles.Select(x => x.Name).ToArray() : new string[] { "Visitor" };

            // Create command result.
            var commandResult = new CommandResult
            {
                Command = commandName.ToUpper(),
                CurrentUser = _currentUser,
                CommandContext = this._commandContext,
                IPAddress = _ipAddress
            };

            // Obtain all commands for the roles the user is a part of.
            var commands = _commands
                .Where(x => x.Roles.Any(y => availableRoles.Any(z => z.Is(y))))
                .ToList();

            foreach (var cmd in commands)
            {
                cmd.CommandResult = commandResult;
                cmd.AvailableCommands = commands;
            }

            if (_currentUser != null && _currentUser.BanInfo != null)
                if (DateTime.UtcNow < _currentUser.BanInfo.EndDate)
                    return BanMessage(commandResult);
                else
                {
                    _userRepository.UnbanUser(_currentUser.Username);
                    _userRepository.UpdateUser(_currentUser);
                }

            // Obtain the command the user intends to execute from the list of available commands.
            var command = commands.SingleOrDefault(x => x.Name.Is(commandName));

            if (commandName.Is("INITIALIZE"))
                this._commandContext.Deactivate();

            // Perform different behaviors based on the current command context.
            switch (this._commandContext.Status)
            {
                // Perform normal command execution.
                case ContextStatus.Disabled:
                    if (command != null)
                    {
                        command.CommandResult.Command = command.Name;
                        command.Invoke(args);
                    }
                    break;

                // Perform normal command execution.
                // If command does not exist, attempt to use command context instead.
                case ContextStatus.Passive:
                    if (command != null)
                    {
                        command.CommandResult.Command = command.Name;
                        command.Invoke(args);
                    }
                    else if (!commandName.Is("HELP"))
                    {
                        command = commands.SingleOrDefault(x => x.Name.Is(this._commandContext.Command));
                        if (command != null)
                        {
                            args = commandString.Contains(' ') ? commandString.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries) : new string[] { commandString };
                            var newArgs = new List<string>();
                            if (this._commandContext.Args != null) newArgs.AddRange(this._commandContext.Args);
                            newArgs.AddRange(args);
                            command.CommandResult.Command = command.Name;
                            command.Invoke(newArgs.ToArray());
                        }
                    }
                    break;

                // Perform command execution using command context.
                // Reset command context if "CANCEL" is supplied.
                case ContextStatus.Forced:
                    if (!commandName.Is("CANCEL"))
                    {
                        command = commands.SingleOrDefault(x => x.Name.Is(this._commandContext.Command));
                        if (command != null)
                        {
                            command.CommandResult.Command = command.Name;
                            if (this._commandContext.Prompt)
                            {
                                var newStrings = new List<string>();
                                if (this._commandContext.PromptData != null)
                                    newStrings.AddRange(this._commandContext.PromptData);
                                newStrings.Add(commandString);
                                this._commandContext.PromptData = newStrings.ToArray();
                                command.Invoke(this._commandContext.Args);
                            }
                            else
                            {
                                args = commandString.Contains(' ') ? commandString.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries) : new string[] { commandString };
                                var newArgs = new List<string>();
                                if (this._commandContext.Args != null) newArgs.AddRange(this._commandContext.Args);
                                newArgs.AddRange(args);
                                args = newArgs.ToArray();
                                command.Invoke(args);
                            }
                        }
                    }
                    else
                    {
                        commandResult.CommandContext.Restore();
                        commandResult.WriteLine("Action canceled.");
                    }
                    break;
            }

            // If command does not exist, check if the command was "HELP".
            // If so, call the ShowHelp method to show help information for all available commands.
            if (command == null)
            {
                if (commandName.Is("HELP"))
                    commandResult = DisplayHelp(commands, args, commandResult);
                else if (!commandName.Is("CANCEL"))
                    if (commandName.IsNullOrWhiteSpace())
                        commandResult.WriteLine("You must supply a command.");
                    else
                        commandResult.WriteLine("'{0}' is not a recognized command or is not available in the current context.", commandName);
            }

            _currentUser = commandResult.CurrentUser;
            if (_currentUser != null && _currentUser.BanInfo != null)
                if (DateTime.UtcNow < _currentUser.BanInfo.EndDate)
                    return BanMessage(commandResult);
                else
                {
                    _userRepository.UnbanUser(_currentUser.Username);
                    _userRepository.UpdateUser(_currentUser);
                }

            // Temporarily notify of messages on each command execution.
            if (_currentUser != null)
            {
                var unreadMessageCount = _messageRepository.UnreadMessages(_currentUser.Username);
                if (unreadMessageCount > 0)
                {
                    commandResult.WriteLine();
                    commandResult.WriteLine("You have {0} unread message(s).", unreadMessageCount);
                }
            }

            commandResult.TerminalTitle = string.Format("Terminal - {0}", _currentUser != null ? _currentUser.Username : "Visitor");

            FinalParsing(commandResult);

            return commandResult;
        }

        //public TopicUpdate GrabTopicUpdate

        #endregion

        #region Private Methods

        /// <summary>
        /// Display help information for all available commands, or invoke the help argument for a specifically supplied command.
        /// </summary>
        /// <param name="commands">The list of available commands.</param>
        /// <param name="args">Any arguments passed in.</param>
        /// <returns>A CommandResult option containing properties relevant to how data should be processed by the UI.</returns>
        private CommandResult DisplayHelp(IEnumerable<ICommand> commands, string[] args, CommandResult commandResult)
        {
            var options = new OptionSet();
            options.Add(
                "?|help",
                "Show help information.",
                x =>
                {
                    HelpUtility.WriteHelpInformation(
                        commandResult,
                        "HELP",
                        "[Option]",
                        "Displays help information.",
                        options
                    );
                }
            );
            try
            {
                if (args != null)
                {
                    var parsedArgs = options.Parse(args);
                    if (parsedArgs.Count == args.Length)
                    {
                        var commandName = parsedArgs.First();
                        var command = commands.SingleOrDefault(x => x.Name.Is(commandName));
                        if (command != null)
                            command.Invoke(new string[] { "-help" });
                        else
                            commandResult.WriteLine("'{0}' is not a recognized command.", commandName);
                    }
                }
                else
                {
                    commandResult.WriteLine("The following commands are available:");
                    commandResult.WriteLine();
                    foreach (ICommand command in commands.OrderBy(x => x.Name))
                        if (command.ShowHelp)
                            commandResult.WriteLine(DisplayMode.DontType, "{0}{1}", command.Name.PadRight(15), command.Description);
                    commandResult.WriteLine();
                    commandResult.WriteLine("Type \"COMMAND -?\" for details on individual commands.");
                    if (_currentUser != null)
                    {
                        commandResult.WriteLine();
                        commandResult.WriteLine(DisplayMode.Dim | DisplayMode.DontType, new string('-', AppSettings.DividerLength));
                        commandResult.WriteLine();
                        var aliases = _aliasRepository.GetAliases(_currentUser.Username);
                        if (aliases.Count() > 0)
                        {
                            commandResult.WriteLine("You have the following aliases defined:");
                            commandResult.WriteLine();
                            foreach (var alias in aliases)
                                commandResult.WriteLine(DisplayMode.DontType, "{0}'{1}'", alias.Shortcut.ToUpper().PadRight(15, ' '), alias.Command);
                        }
                        else
                            commandResult.WriteLine("You have no aliases defined.");
                    }
                }
            }
            catch (OptionException ex)
            {
                commandResult.WriteLine(ex.Message);
            }

            return commandResult;
        }

        private CommandResult BanMessage(CommandResult commandResult)
        {
            commandResult.Display.Clear();
            commandResult.WriteLine("You were banned by {0}.", _currentUser.BanInfo.Creator);
            commandResult.WriteLine();
            commandResult.WriteLine("Reason: {0}", _currentUser.BanInfo.Reason);
            commandResult.WriteLine();
            commandResult.WriteLine("Expires {0}.", _currentUser.BanInfo.EndDate.TimeUntil());
            FinalParsing(commandResult);
            return commandResult;
        }

        private void FinalParsing(CommandResult commandResult)
        {
            foreach (var displayItem in commandResult.Display)
            {
                if (_currentUser != null && !_currentUser.Sound)
                    displayItem.DisplayMode |= DisplayMode.Mute;

                if (ParseAsHtml)
                {
                    if ((displayItem.DisplayMode & DisplayMode.Parse) != 0)
                        displayItem.Text = BBCodeUtility.ConvertTagsToHtml(displayItem.Text);
                    else
                        displayItem.Text = HttpUtility.HtmlEncode(displayItem.Text);

                    var masterParser = new BBCodeParser(new[]
                    {
                        new BBTag("transmit", "<span class='transmit' transmit='${transmit}'>", "</span>", new BBAttribute("transmit", "")),
                        new BBTag("topicboardid", "<span id='topicboardid-${topicId}'>", "</span>", new BBAttribute("topicId", "")),
                        new BBTag("topicstatus", "<span id='topicstatus-${topicId}'>", "</span>", new BBAttribute("topicId", "")),
                        new BBTag("topictitle", "<span id='topictitle-${topicId}'>", "</span>", new BBAttribute("topicId", "")),
                        new BBTag("replycount", "<span id='replycount-${topicId}'>", "</span>", new BBAttribute("topicId", "")),
                        new BBTag("topicbody", "<div id='topicbody-${topicId}'>", "</div>", new BBAttribute("topicId", "")),
                        new BBTag("topicmaxpages", "<span id='topicmaxpages-${topicId}'>", "</span>", new BBAttribute("topicId", "")),
                    });

                    displayItem.Text = masterParser.ToHtml(displayItem.Text, false);

                    displayItem.Text = displayItem.Text
                        .Replace("\n", "<br />")
                        .Replace("\r", "")
                        .Replace("  ", " &nbsp;");

                    string cssClass = null;
                    if ((displayItem.DisplayMode & DisplayMode.Inverted) != 0)
                    {
                        cssClass += "inverted ";
                        displayItem.Text = string.Format("&nbsp;{0}&nbsp;", displayItem.Text);
                    }
                    if ((displayItem.DisplayMode & DisplayMode.Dim) != 0)
                        cssClass += "dim ";
                    if ((displayItem.DisplayMode & DisplayMode.Italics) != 0)
                        cssClass += "italics ";
                    if ((displayItem.DisplayMode & DisplayMode.Bold) != 0)
                        cssClass += "bold ";
                    displayItem.Text = string.Format("<span class='{0}'>{1}</span>", cssClass, displayItem.Text);
                }
                else
                {
                    var masterParser = new BBCodeParser(new[]
                    {
                        new BBTag("transmit", "", "", new BBAttribute("transmit", "")),
                        new BBTag("topicboardid", "", "", new BBAttribute("topicId", "")),
                        new BBTag("topicstatus", "", "", new BBAttribute("topicId", "")),
                        new BBTag("topictitle", "", "", new BBAttribute("topicId", "")),
                        new BBTag("replycount", "", "", new BBAttribute("topicId", "")),
                        new BBTag("topicbody", "", "", new BBAttribute("topicId", "")),
                        new BBTag("topicmaxpages", "", "", new BBAttribute("topicId", "")),
                    });

                    displayItem.Text = masterParser.ToHtml(displayItem.Text, false);

                    if ((displayItem.DisplayMode & DisplayMode.DontWrap) == 0)
                        displayItem.Text = displayItem.Text.WrapOnSpace(AppSettings.MaxLineLength);
                }
            }
        }

        #endregion
    }
}
