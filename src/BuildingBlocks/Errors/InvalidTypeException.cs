using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.Errors
{
    public sealed class InvalidTypeException : Exception
    {
        public InvalidTypeException(string message) : base(message) { }
    }
}
