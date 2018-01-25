using BaseModel.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

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


        /// <summary>
        /// Parse string into string only value and number only value.
        /// </summary>
        /// <param name="fullStringValue">String to parse.</param>
        /// <param name="numericFieldlength">Length of number value.</param>
        /// <param name="numberComponent">Parsed number value.</param>
        /// <returns></returns>
        public static string ParseStringIntoComponents(string fullStringValue, out int numericFieldlength, out long numberComponent)
        {
            int numberLength = 0;
            int? numericIndex = StringFormatUtils.GetNumericIndex(fullStringValue, out numberLength);
            if (fullStringValue == null || fullStringValue == string.Empty)
            {
                numberComponent = 0;
                numericFieldlength = 0;
                return string.Empty;
            }

            string stringOnlyValue = fullStringValue.Substring(0, fullStringValue.Length - numberLength);
            if (numericIndex != null)
                numberComponent = Int64.Parse(fullStringValue.Substring(numericIndex.Value, fullStringValue.Length - numericIndex.Value));
            else
                numberComponent = 0;

            numericFieldlength = numberLength;

            return stringOnlyValue;
        }

        public static string GetNewRegisterNumber(IEnumerable<IEntityNumber> originalEntities, IEnumerable<IEntityNumber> unsavedEntities, string duplicateInternalNumber, IEnumerable<IEntityNumber> insertSelectedEntities, string entityGroup = "")
        {
            if (duplicateInternalNumber != string.Empty && duplicateInternalNumber != null)
            {
                string stringValueToFill = duplicateInternalNumber;
                int numericFieldLength = 0;
                long valueToFillNumberOnly = 0;
                string valueToFillStringOnly = ParseStringIntoComponents(duplicateInternalNumber, out numericFieldLength, out valueToFillNumberOnly);

                List<IEntityNumber> allEntities = new List<IEntityNumber>(originalEntities);
                allEntities.AddRange(unsavedEntities);

                List<string> originalEntitiesSimilarNames =
                originalEntities.Where(x => x.EntityGroup == entityGroup).Where(x => x.EntityNumber != null).Select(x => x.EntityNumber).ToList();

                List<string> allEntitiesSimilarNames =
                allEntities.Where(x => x.EntityGroup == entityGroup).Where(x => x.EntityNumber != null).Select(x => x.EntityNumber).ToList();

                List<string> unsavedEntitiesSimilarNames =
                unsavedEntities.Where(x => x.EntityGroup == entityGroup).Where(x => x.EntityNumber != null).Select(x => x.EntityNumber).ToList();

                List<string> insertSelectedEntitiesSimilarNames = insertSelectedEntities.Where(x => x.EntityGroup == entityGroup).Where(x => x.EntityNumber != null).Select(x => x.EntityNumber).ToList();

                do
                {
                    valueToFillNumberOnly += 1;
                    string nextName = StringFormatUtils.AppendStringWithEnumerator(valueToFillStringOnly, valueToFillNumberOnly, numericFieldLength);

                    bool isExistsInInsert = insertSelectedEntitiesSimilarNames.Any(x => x == nextName);
                    bool isExistsInUnsaved = unsavedEntitiesSimilarNames.Any(x => x == nextName);

                    //when current name exists in unsaved it means that nextName is not safe to be used
                    if (isExistsInUnsaved)
                        continue;
                    //when current name exists in insert it means that nextName is not safe to be used
                    else if (isExistsInInsert)
                        continue;
                    else
                        return nextName;

                } while (valueToFillNumberOnly < 1000000);
            }

            return string.Empty;
        }
    }
}