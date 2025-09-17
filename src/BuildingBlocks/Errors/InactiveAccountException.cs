using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.Errors
{
    public sealed class InactiveAccountException : Exception
    {
        public InactiveAccountException(string message) : base(message) { }
    }
}
