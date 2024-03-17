using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager.Helpers
{
    public static class StringUtils
    {
        /// <summary>
        /// Attempts to parse string into a few common value types
        /// </summary>
        public static T ConvertParse<T>(this string self) where T : IConvertible
        {
            Type type = typeof(T);
            IConvertible valueConverted;

            //Parse the data into the specified data type
            if (type == typeof(bool))
                valueConverted = bool.Parse(self);
            else if (type == typeof(int))
                valueConverted = int.Parse(self);
            else if (type == typeof(float))
                valueConverted = float.Parse(self);
            else if (type == typeof(string))
                valueConverted = self;
            else
                throw new NotSupportedException(type + " is not able to be converted");
            return (T)valueConverted;
        }
    }
}
