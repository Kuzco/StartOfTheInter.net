using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Terminal.Domain.Entities;
using Terminal.Domain.Repositories.Interfaces;
using SignalR.Hubs;

namespace Terminal.Domain.Hubs
{
    public class TerminalHub : Hub
    {
        private TerminalCore _terminalCore;

        public TerminalHub(TerminalCore terminalCore)
        {
            _terminalCore = terminalCore;
        }

        public void Send(string data)
        {
            Clients.addMessage(_terminalCore.Username + ": " + data);
        }
    }
}