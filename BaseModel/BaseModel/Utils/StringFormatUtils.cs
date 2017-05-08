using System;
using System.Collections.Generic;

namespace BaseModel.Helpers
{
    public static class StringFormatUtils
    {
        /// <summary>
        /// Separate parts of string to alphabets and enumerated numbers starting from the end
        /// </summary>
        /// <param name="stringToExtractNumbers">the entire numbering string</param>
        /// <param name="numericFieldLength">length calculated from the end of the string indicating where enumeration happens</param>
        /// <returns></returns>
        public static int? GetNumericIndex(string stringToExtractNumbers, out int numericFieldLength)
        {
            //string pattern = @"\d+$";
            //Regex rgx = new Regex(pattern);
            //var matches = rgx.Match(stringToExtractNumbers);
            //if (matches.Value == string.Empty)
            //    return null;

            numericFieldLength = 0;
            if (stringToExtractNumbers == null || stringToExtractNumbers == string.Empty)
                return null;

            var stack = new Stack<char>();
            int? returnValue = null;
            bool isLeadingZeros = false;
            for (var i = stringToExtractNumbers.Length - 1; i >= 0; i--)
            {
                char extractChar = stringToExtractNumbers[i];
                if (!char.IsNumber(extractChar))
                    return returnValue;

                numericFieldLength += 1;
                if (extractChar == '0' && isLeadingZeros)
                    continue;
                else
                {
                    //any zeros from here onwards are classified as leading zeros
                    isLeadingZeros = true;
                    returnValue = i;
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Extract parts of the enumerated string and append it back with the same amount of length allocated to the string from the right
        /// </summary>
        /// <param name="stringPortionOnly">string portion to append</param>
        /// <param name="enumerator">current numeric value to append to string</param>
        /// <param name="numericFieldLength">portion of string allocated for enumeration from the right</param>
        /// <returns></returns>
        public static string AppendStringWithEnumerator(string stringPortionOnly, long enumerator, int numericFieldLength)
        {
            string enumeratorString = enumerator.ToString();
            int numberOfLeadingZeros = numericFieldLength - enumeratorString.Length;
            for (int i = 0; i < numberOfLeadingZeros; i++)
            {
                stringPortionOnly += "0";
            }
            return stringPortionOnly += enumeratorString;
        }
    }

}