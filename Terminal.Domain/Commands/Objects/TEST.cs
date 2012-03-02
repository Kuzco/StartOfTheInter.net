using System.Collections.Generic;
using Terminal.Domain.Objects;
using Terminal.Domain.Commands.Interfaces;
using Terminal.Domain.Entities;
using Terminal.Domain.Settings;
using SignalR.Hosting.AspNet;
using SignalR.Infrastructure;
using SignalR;
using Terminal.Domain.Hubs;

namespace Terminal.Domain.Commands.Objects
{
    public class TEST : ICommand
    {
        private EntityContainer _entityContainer;

        public TEST(EntityContainer entityContainer)
        {
            _entityContainer = entityContainer;
        }

        public CommandResult CommandResult { get; set; }

        public IEnumerable<ICommand> AvailableCommands { get; set; }

        public string[] Roles
        {
            get { return RoleTemplates.Everyone; }
        }

        public string Name
        {
            get { return "TEST"; }
        }

        public string Parameters
        {
            get { return "[Option(s)]"; }
        }

        public string Description
        {
            get { return "Development only."; }
        }

        public bool ShowHelp
        {
            get { return true; }
        }

        public void Invoke(string[] args)
        {
            var connectionManager = AspNetHost.DependencyResolver.Resolve<IConnectionManager>();
            dynamic clients = connectionManager.GetClients<TerminalHub>();
            clients.addMessage("Test command was invoked.");
        }
    }
}
