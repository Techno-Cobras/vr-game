namespace VrGame.Data.Items
{
    internal static class StableId
    {
        public static bool IsValid(string value, string requiredPrefix)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(requiredPrefix, System.StringComparison.Ordinal))
                return false;

            var segmentHasCharacter = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == '.')
                {
                    if (!segmentHasCharacter || value[index - 1] == '-' || value[index - 1] == '_')
                        return false;
                    segmentHasCharacter = false;
                    continue;
                }

                var isAlphaNumeric = (character >= 'a' && character <= 'z') ||
                                     (character >= '0' && character <= '9');
                if (!isAlphaNumeric && character != '-' && character != '_')
                    return false;
                if (!isAlphaNumeric && !segmentHasCharacter)
                    return false;

                segmentHasCharacter = true;
            }

            var lastCharacter = value[value.Length - 1];
            return segmentHasCharacter && lastCharacter != '-' && lastCharacter != '_';
        }
    }
}
