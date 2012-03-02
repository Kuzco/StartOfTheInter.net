using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Terminal.Domain.Entities;

namespace Terminal.Domain.Repositories.Interfaces
{
    /// <summary>
    /// A repository for storing application variables.
    /// </summary>
    public interface IVariableRepository
    {
        /// <summary>
        /// Adds or edits a variable in the repository.
        /// </summary>
        /// <param name="variable">The variable to be added.</param>
        void ModifyVariable(Variable variable);

        /// <summary>
        /// Gets a variable from the variable repository.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        Variable GetVariable(string name);
    }
}