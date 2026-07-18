using System;

namespace _Project.Scripts.Systems.Resources
{
    /// <summary>
    /// Maps scene resource types to stable inventory ids used by configs and runtime systems.
    /// </summary>
    public static class SceneResourceTypeExtensions
    {
        public static string GetResourceId(this SceneResourceType resourceType)
        {
            switch (resourceType)
            {
                case SceneResourceType.Aluminium:
                    return "aluminium";
                case SceneResourceType.WaterPipe:
                    return "Water Pipe";
                case SceneResourceType.OxygenPipe:
                    return "Oxygen Pipe";
                default:
                    return resourceType.ToString();
            }
        }

        public static bool TryParseResourceId(string resourceId, out SceneResourceType resourceType)
        {
            resourceType = SceneResourceType.Iron;
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            if (string.Equals(resourceId, "Water Pipe", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = SceneResourceType.WaterPipe;
                return true;
            }

            if (string.Equals(resourceId, "Oxygen Pipe", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = SceneResourceType.OxygenPipe;
                return true;
            }

            if (string.Equals(resourceId, "aluminium", StringComparison.OrdinalIgnoreCase)
                || string.Equals(resourceId, "Aluminium", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = SceneResourceType.Aluminium;
                return true;
            }

            return Enum.TryParse(resourceId, true, out resourceType);
        }
    }
}
