using System.Collections.Generic;
using Terminal.Domain.Objects;
using Terminal.Domain.Commands.Interfaces;
using Terminal.Domain.Entities;
using Terminal.Domain.Settings;
using SignalR.Hosting.AspNet;
using SignalR.Infrastructure;
using SignalR;
using Terminal.Domain.Hubs;
using Terminal.Domain.Repositories.Interfaces;
using Mono.Options;
using Terminal.Domain.Utilities;
using System;
using Terminal.Domain.ExtensionMethods;

namespace Terminal.Domain.Commands.Objects
{
    // A template for creating new commands.
    // Copying this class should make it easier to create new commands.
    // To adhere to convention all command classes should be in all upper case.
    public class SAMPLE : ICommand
    {
        // This region is a for asking for and storing all dependencies.
        // The included example shows an instance of IUserRepository being inject.
        // All you have to do is ask for it in the constructor and Ninject will take
        // care of injecting it for you.
        #region Setup

        private IVariableRepository _variableRepository;

        public SAMPLE(IVariableRepository variableRepository)
        {
            _variableRepository = variableRepository;
        }

        #endregion

        // This region contains all the properties that we are required to have by the
        // ICommand interface.
        #region Required properties

        // The command result is set by the terminal core.
        // You do not have to set this to anything, but you will need to use it to return data.
        public CommandResult CommandResult { get; set; }

        // The terminal core is aware of what commands are available to the currently logged in user.
        // This property will be populated with a list of the available commands.
        // Don't forget to check for nulls if you iterate over this list looking for a specific command;
        // if the user does not have access to that command as part of their role then it will not exist.
        public IEnumerable<ICommand> AvailableCommands { get; set; }

        // A string array of roles that this command is part of.
        // 
        // You can return an actual string array containing the roles that have access to this command:
        // "return new string[] { "Visitor", "User", "Moderator", "Administrator" };"
        // 
        // Or you can use one of the pre-defined templates in Domain.Settings:
        // "return RoleTemplates.Everyone;"
        // "return RoleTemplates.Visitor;"
        // "return RoleTemplates.OnlyUsers;"
        // "return RoleTemplates.ModsAndUsers;"
        // "return RoleTemplates.AllLoggedIn;"
        public string[] Roles
        {
            get { return RoleTemplates.Everyone; }
        }

        // The name of this command. Usually the same as the class name.
        public string Name
        {
            get { return "SAMPLE"; }
        }

        // Meta information about available command options.
        // Square brackets "[ ]" usually wrap optional sections while angle brackets "< >" usually wrap required parameters.
        public string Parameters
        {
            get { return "[Option(s)]"; }
        }

        // A breif description of the command for display in the help menu.
        public string Description
        {
            get { return "A sample command that can be used as a template to create new commands."; }
        }

        // Return "true" if this command should show up in the help menu.
        // Setting this to false is an easy way to create a hidden command.
        public bool ShowHelp
        {
            get { return true; }
        }

        #endregion

        // This is where the magic happens.
        // Many different things can happen in a command's "Invoke" method.
        // I will show a few simple examples, but it is important to keep in mind that this
        // method can rapidly grown in complexity just like any complex console application.
        // 
        // Think of "Invoke" like the "Main" method of a console app.
        public void Invoke(string[] args)
        {
            // One common thing to do in command classes is to setup Mono.Options to help parse
            // arguments passed in by the user. I will include a few different examples.
            #region Mono.Options Argument Setup

            // Instantiate the option set used by Mono.Options.
            var options = new OptionSet();

            // The most common option to add to Mono.Options is a help option to show more detailed help
            // information about the specific command and its available options.
            //
            // We are setting up a boolean which will be set to true if the user supplied the -? or -help arguments.
            // For more information on how Mono.Options (originally NDesk.Options) parses arguments in a string array,
            // visit http://www.ndesk.org/Options.
            bool showHelp = false;
            options.Add(
                "?|help",
                "Show help information.",
                x => showHelp = true
            );

            // Some options have values. Those values can either be optional or required. To specify that the option
            // requires a value you simply place an equals sign after the option prototype like below. Then specify a variable
            // to set the value of when Mono.Options runs the lambda expression. 'x' being the supplied string value from the user.
            int count = 0;
            options.Add(
                "c|count=",
                "Count to a specified number.",
                x => count = x.ToInt()
            );

            // Now let's do a command with an optional value. To specify optional values you add a colon after the prototype like below.
            // In this instance we need to do two things. Set our boolean to true so we know it got executed, and then set a string to
            // the user-supplied value. If the user did not supply a value then it will be null. Luckily we are able to use statement
            // block lambdas with Mono.Options so we can perform multiple functions.
            bool echo = false;
            string echoValue = null;
            options.Add(
                "echo:",
                "Echo a default message or a specified value.",
                x =>
                {
                    echo = true;
                    echoValue = x;
                }
            );

            // Unfortunately one failing of Mono.Options is that arguments with optional or required values cannot have spaces in the
            // supplied values. A space to Mono.Options is a signal that a new argument is being supplied. To get around this we use
            // a Terminal mode called a prompt.
            //
            // A prompt is a mode where the user will be prompted to supply a value after supplying a specific command.
            // Example: Using "TOPIC 123 -r" begins a prompt asking for the user's reply to a topic with ID number 123.
            bool madlib = false;
            options.Add(
                "madlib",
                "Asks the user to supply a few values and then inserts them into a short story.",
                x => madlib = true
            );

            // Let's take everything we've seen so far and add a couple options that will interact with the database so you can see a quick
            // example of how that will work.
            bool readMessage = false;
            options.Add(
                "readMessage",
                "Retrieves a message we stored in the database.",
                x => readMessage = true
            );
            bool writeMessage = false;
            options.Add(
                "writeMessage",
                "Writes a message to the database for later retrieval.",
                x => writeMessage = true
            );

            // Now let's add a few basic arguments that could all be specified together (mutual arguments). The are most often
            // binary values like our help argument, they will either be true or false. I like to use them to toggle options on
            // and off. For example: locking and stickying a thread at the same time. I could specify the -lock argument and the
            // -sticky argument on the TOPIC command and both functions would be performed. Or I could specify only one of the arguments.
            //
            // I use nullable booleans here because they are null if Mono.Options never sees them. If it does see them then it will
            // either be true or false based on whether or not the user supplied a minus after the argument (explicitly setting it
            // to false). 'x' will be null if the minus is used, otherwise it will have the option name as its value. We can use
            // this to determine if our boolean should be true or false.
            bool? option1 = null;
            bool? option2 = null;
            options.Add(
                "option1",
                "Toggles option1 on. Supply a minus (-) after the option to toggle it off.",
                x => option1 = x != null
            );
            options.Add(
                "option2",
                "Toggles option2 on. Supply a minus (-) after the option to toggle it off.",
                x => option2 = x != null
            );

            #endregion

            // After setting up the available arguments we will then need to parse the arguments
            // supplied by the user and execute our logic based on them.
            #region Execution Logic

            // If your command functions without any arguments supplied, this is where you should put that logic.
            // Many commands require arguments to function properly. If that is the case for your command then simply
            // write out an "Invalid arguments supplied" message, which is conveniently stored in a static string
            // display template.
            if (args == null)
            {
                this.CommandResult.WriteLine(DisplayTemplates.InvalidArguments);
            }

            // If your command does require arguments to function then that logic belongs here.
            else
            {
                // If Mono.Options detects an error when parsing, such as not being able to find a value for a supplied
                // option that requires a value, it will throw an exception. We should catch that exception and write the
                // exception message to the command result.
                try
                {
                    // Tell Mono.Options to parse our string array of arguments. For each argument that matches
                    // one of the options we added to our option set above, Mono.Options will execute the relevant
                    // lambda expression (which we used to set a boolean).
                    //
                    // It will return a list of arguments that did not match an option in the option set.
                    // This is useful for things like ID numbers or page numbers. All the arguments that matched
                    // an option will not be part of the resulting list.
                    var parsedArgs = options.Parse(args).ToArray();

                    // Check if the list of unmatched arguments has the same count as the original list of arguments.
                    // If it does then Mono.Options did not find any matching options and did not set any boolean flags.
                    // If you were expecting this then put logic for that scenario here. However if you were expecting
                    // Mono.Options to set some flags then show an invalid argument message here.
                    if (parsedArgs.Length == args.Length)
                    {
                        this.CommandResult.WriteLine(DisplayTemplates.InvalidArguments);
                    }

                    // If the pasredArgs count is less than args then Mono.Options found and executed some options for us.
                    // We should check our boolean flags here.
                    else
                    {
                        // Some options are exclusive, meaning that if they are executed then you don't want to execute any others.
                        // Example: If the user supplied both the -? option and another option that displays a lot of information
                        // then it would be a little silly to execute both options. To handle this we handle all exclusive options
                        // first with IF and ELSE IF statements. Then mutual options (options that should be executed even if multiple
                        // options are supplied) should be checked in the ELSE statement.
                        //
                        // If our showHelp flag is true then we want to display detailed help information about the command and its options.
                        // Lucky for us that Mono.Options will automatically spit out formatted help text for all of the options we added
                        // to the option set above. I have written a helpful help utility to print out he detailed help message. This way
                        // if you ever need to change the help wording or something, you simply modify the WriteHelpInformation method
                        // rather than changing this logic in every single command.
                        if (showHelp)
                        {
                            HelpUtility.WriteHelpInformation(
                                this.CommandResult,
                                this.Name,
                                this.Parameters,
                                this.Description,
                                options
                            );
                        }

                        // Check to see if our count argument was supplied and if so then perform an action using it's value.
                        else if (count > 0)
                        {
                            for (var x = 1; x <= count; x++)
                            {
                                this.CommandResult.WriteLine("Iteration {0}", x);
                            }
                        }

                        // Check to see if our echo argument was supplied and if so then perform an action using it's value,
                        // or if no value was supplied then use a default value.
                        else if (echo)
                        {
                            string message = echoValue ?? "Hello_World!";
                            this.CommandResult.WriteLine("Echo: {0}", message);
                        }

                        // Check to see if our madlib argument was supplied and if so then begin a prompt to ask for more user
                        // user-supplied data.
                        else if (madlib)
                        {
                            // When the SetPrompt method is called on the command context the same command and arguments will be passed
                            // on the next request and all of the user-supplied text will be stored directly in a prompt string array
                            // on the command context. You can use this array to determine how far along you are in your prompt.
                            // Just don't forget to call the Restore or Deactive method when you finish the prompt or the user will be
                            // stuck on the last prompt you created until they type cancel or reload the terminal.
                            //
                            // Restore attempts to restore the context the user was in before the prompt so it is preferred. But if you
                            // want to nuke the entire command context you can call Deactive. Both should remove the user from prompt
                            // mode.
                            if (this.CommandResult.CommandContext.PromptData == null)
                            {
                                this.CommandResult.WriteLine("Enter a noun.");
                                this.CommandResult.CommandContext.SetPrompt(this.Name, args, "MADLIB - Supply a noun");
                            }
                            else if (this.CommandResult.CommandContext.PromptData.Length == 1)
                            {
                                this.CommandResult.WriteLine("Enter a past-tense verb.");
                                this.CommandResult.CommandContext.SetPrompt(this.Name, args, "MADLIB - Supply a past-tense verb");
                            }
                            else if (this.CommandResult.CommandContext.PromptData.Length == 2)
                            {
                                this.CommandResult.WriteLine("Enter an adjective.");
                                this.CommandResult.CommandContext.SetPrompt(this.Name, args, "MADLIB - Supply an adjective");
                            }
                            else if (this.CommandResult.CommandContext.PromptData.Length == 3)
                            {
                                var noun = this.CommandResult.CommandContext.PromptData[0];
                                var verb = this.CommandResult.CommandContext.PromptData[1];
                                var adj = this.CommandResult.CommandContext.PromptData[2];
                                this.CommandResult.WriteLine("Madlib result: The {0} {1} {2} over the lazy dog.", adj, noun, verb);
                                this.CommandResult.CommandContext.Restore();
                            }
                        }
                        
                        // Check to see if our writeMessage argument was supplied and if so then prompt the user for a message and
                        // store it in the variables table in the database.
                        else if (writeMessage)
                        {
                            if (this.CommandResult.CommandContext.PromptData == null)
                            {
                                this.CommandResult.WriteLine("Enter a message to store in the database.");
                                this.CommandResult.CommandContext.SetPrompt(this.Name, args, "Write Message");
                            }
                            else if (this.CommandResult.CommandContext.PromptData.Length == 1)
                            {
                                var message = this.CommandResult.CommandContext.PromptData[0];
                                _variableRepository.ModifyVariable("sampleMessage", message);
                                this.CommandResult.WriteLine("Message saved successfully.");
                                this.CommandResult.CommandContext.Restore();
                            }
                        }

                        // Check to see if our readMessage arugment was supplied and if so then read the value of our message from
                        // the database and display it.
                        else if (readMessage)
                        {
                            var message = _variableRepository.GetVariable("sampleMessage") ?? "You have not stored a message yet.";
                            this.CommandResult.WriteLine("Result: {0}", message);
                        }

                        // Now check all mutual arguments. All the arguments in here could be specified together or separately.
                        // Depending on what you're trying to accomplish you may have more arguments above in the IF, ELSE IF
                        // section, or you might have more in the mutual section. It's totally up to you. Some people may prefer
                        // that their users be able to show help, count, and echo all in the same command. It's entirely your
                        // own preference.
                        else
                        {
                            if (option1 != null)
                                this.CommandResult.WriteLine("Option1 has been {0}.", (bool)option1 ? "enabled" : "disabled");

                            if (option2 != null)
                                this.CommandResult.WriteLine("Option2 has been {0}.", (bool)option2 ? "enabled" : "disabled");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Write the exception message to the command result for display on the client.
                    this.CommandResult.WriteLine(ex.Message);
                }
            }

            #endregion
        }
    }
}
