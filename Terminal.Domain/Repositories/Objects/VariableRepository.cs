﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminal.Domain.Repositories.Interfaces;
using Terminal.Domain.Entities;
using Terminal.Domain.ExtensionMethods;

namespace Terminal.Domain.Repositories.Objects
{
    public class VariableRepository : IVariableRepository
    {
        private EntityContainer _entityContainer;

        public VariableRepository(EntityContainer entityContainer)
        {
            _entityContainer = entityContainer;
        }

        public void ModifyVariable(string name, string value)
        {
            var variable = _entityContainer.Variables.SingleOrDefault(x => x.Name.Equals(name));
            if (variable == null)
                _entityContainer.Variables.Add(new Variable { Name = name, Value = value });
            else
                variable.Value = value;

            _entityContainer.SaveChanges();
        }

        public string GetVariable(string name)
        {
            var variable = _entityContainer.Variables.SingleOrDefault(x => x.Name.Equals(name));
            if (variable != null)
                return variable.Value;
            return null;
        }
    }
}
