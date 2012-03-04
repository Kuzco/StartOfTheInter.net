﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminal.Domain.Enums;

namespace Terminal.Domain.Objects
{
    /// <summary>
    /// The command context describes the current state of the terminal.
    /// It helps the terminal core make decisions about how to execute commands.
    /// </summary>
    [Serializable]
    public class CommandContext
    {
        /// <summary>
        /// The currently contexted command.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// The currently contexted arguments.
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// The console text to display for the current context.
        /// </summary>
        public string Text { get; set; }

        public bool Prompt { get; set; }

        /// <summary>
        /// Custom string array for storing multi-word values from prompts.
        /// </summary>
        public string[] PromptData { get; set; }

        /// <summary>
        /// The currently displayed page.
        /// </summary>
        public int CurrentPage { get; set; }

        public List<string> CurrentLinkTags { get; set; }

        public List<string> CurrentSearchTerms { get; set; }

        public string CurrentSortOrder { get; set; }

        /// <summary>
        /// The current status of the context.
        /// 
        /// Options:
        /// 
        /// Disabled - The context is disabled.
        /// Passive - The context contains data but normal command execution should be attempted first.
        /// Forced - The context has data and must be used for command execution.
        /// </summary>
        public ContextStatus Status { get; set; }

        /// <summary>
        /// Stores the previous command context when the Backup method is called.
        /// </summary>
        public CommandContext PreviousContext { get; set; }

        /// <summary>
        /// Sets up a prompt where all data from the command line will be dumped into the PromptData collection.
        /// </summary>
        /// <param name="command">The command to be set as the contexted command.</param>
        /// <param name="args">The arguments to be set as the contexted arguments.</param>
        /// <param name="text">The custom text to display next to the command line.</param>
        public void SetPrompt(string command, string[] args, string text)
        {
            this.Prompt = true;
            this.Set(ContextStatus.Forced, command, args, text);
        }

        /// <summary>
        /// Set the current command context.
        /// </summary>
        /// <param name="status">The status of the context you are setting.</param>
        /// <param name="command">The command to be set as the contexted command.</param>
        /// <param name="args">The arguments to be set as the contexted arguments.</param>
        /// <param name="text">The custom text to display next to the command line.</param>
        public void Set(ContextStatus status, string command, string[] args, string text)
        {
            if (this.Status == ContextStatus.Passive)
                if (status == ContextStatus.Forced)
                    this.Backup();
            this.Status = status;
            this.Command = command;
            this.Args = args;
            this.Text = text;
        }

        /// <summary>
        /// Save the current command context as the previous command context.
        /// </summary>
        private void Backup()
        {
            this.PreviousContext = new CommandContext
            {
                Status = this.Status,
                Command = this.Command,
                Args = this.Args,
                Text = this.Text,
                Prompt = this.Prompt,
                PromptData = this.PromptData,
                PreviousContext = this.PreviousContext
            };
        }

        /// <summary>
        /// Restore the current command context to the state of the previous command context.
        /// </summary>
        public void Restore()
        {
            if (this.PreviousContext != null)
            {
                this.Status = this.PreviousContext.Status;
                this.Command = this.PreviousContext.Command;
                this.Args = this.PreviousContext.Args;
                this.Text = this.PreviousContext.Text;
                this.Prompt = this.PreviousContext.Prompt;
                this.PromptData = this.PreviousContext.PromptData;
                this.PreviousContext = this.PreviousContext.PreviousContext;
            }
            else
            {
                Deactivate();
            }
        }

        /// <summary>
        /// Disable the current command context.
        /// </summary>
        public void Deactivate()
        {
            this.Status = ContextStatus.Disabled;
            this.Command = null;
            this.Args = null;
            this.Text = null;
            this.Prompt = false;
            this.PromptData = null;
            this.CurrentPage = 0;
            this.CurrentLinkTags = null;
            this.CurrentSearchTerms = null;
            this.CurrentSortOrder = null;
        }
    }
}
