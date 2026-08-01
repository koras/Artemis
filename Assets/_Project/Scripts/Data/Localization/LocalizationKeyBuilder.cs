using System;
using System.Text;

namespace _Project.Scripts.Data.Localization
{
    /// <summary>
    /// Builds localization keys from stable code identifiers.
    /// </summary>
    public static class LocalizationKeyBuilder
    {
        public static string FromEnum(string prefix, Enum value)
        {
            return $"{prefix.Trim('.')}.{ToSnakeCase(value.ToString())}";
        }

        private static string ToSnakeCase(string value)
        {
            StringBuilder result = new StringBuilder(value.Length + 4);

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isUppercase = char.IsUpper(character);
                var @char = value[index + 1];

                bool startsNewWord = index > 0
                                     && isUppercase
                                     && (char.IsLower(value[index - 1])
                                         || index + 1 < value.Length && char.IsLower(@char));

                if (startsNewWord)
                {
                    result.Append('_');
                }

                result.Append(char.ToLowerInvariant(character));
            }

            return result.ToString();
        }
    }
}