namespace _Project.Scripts.Systems.Water
{
    public enum WaterVisualShapeId : byte
    {
        Single = 0,
        End = 1,
        Straight = 2,
        Corner = 3,
        Tee = 4,
        Cross = 5
    }

    /// <summary>
    /// Определяет визуальную форму кабеля по 4-направленной маске.
    /// </summary>
    public static class WaterVisualResolver
    {
        /// <summary>
        /// Возвращает стабильный ключ формы кабеля для маппинга на спрайт.
        /// </summary>
        public static string ResolveShapeKey(byte WaterMask4)
        {
            WaterDirectionMask mask = (WaterDirectionMask)WaterMask4;
            return mask switch
            {
                WaterDirectionMask.Up | WaterDirectionMask.Down => "Vertical",
                WaterDirectionMask.Left | WaterDirectionMask.Right => "Horizontal",
                WaterDirectionMask.Up | WaterDirectionMask.Right => "CornerUpRight",
                WaterDirectionMask.Right | WaterDirectionMask.Down => "CornerRightDown",
                WaterDirectionMask.Down | WaterDirectionMask.Left => "CornerDownLeft",
                WaterDirectionMask.Left | WaterDirectionMask.Up => "CornerLeftUp",
                WaterDirectionMask.Up | WaterDirectionMask.Left | WaterDirectionMask.Right => "TNoDown",
                WaterDirectionMask.Right | WaterDirectionMask.Down | WaterDirectionMask.Left => "TNoUp",
                WaterDirectionMask.Down | WaterDirectionMask.Left | WaterDirectionMask.Up => "TNoRight",
                WaterDirectionMask.Up | WaterDirectionMask.Down | WaterDirectionMask.Right => "TNoLeft",
                WaterDirectionMask.Up | WaterDirectionMask.Right | WaterDirectionMask.Down | WaterDirectionMask.Left => "Cross",
                WaterDirectionMask.Up => "EndUp",
                WaterDirectionMask.Right => "EndRight",
                WaterDirectionMask.Down => "EndDown",
                WaterDirectionMask.Left => "EndLeft",
                _ => "Single"
            };
        }

        /// <summary>
        /// Возвращает форму, угол поворота и русское имя для отладки visual-тайла кабеля.
        /// </summary>
        public static void ResolveVisualDebug(byte WaterMask4, out WaterVisualShapeId shapeId, out float rotationZ, out string shapeNameRu)
        {
            WaterDirectionMask mask = (WaterDirectionMask)WaterMask4;
            rotationZ = 0f;

            switch (mask)
            {
                case 0:
                    shapeId = WaterVisualShapeId.Single;
                    shapeNameRu = "Одиночный (без соседей)";
                    return;
                case WaterDirectionMask.Up:
                    shapeId = WaterVisualShapeId.End;
                    shapeNameRu = "Конец (вверх)";
                    return;
                case WaterDirectionMask.Right:
                    shapeId = WaterVisualShapeId.End;
                    rotationZ = -90f;
                    shapeNameRu = "Конец (вправо)";
                    return;
                case WaterDirectionMask.Down:
                    shapeId = WaterVisualShapeId.End;
                    rotationZ = 180f;
                    shapeNameRu = "Конец (вниз)";
                    return;
                case WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.End;
                    rotationZ = 90f;
                    shapeNameRu = "Конец (влево)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Down:
                    shapeId = WaterVisualShapeId.Straight;
                    shapeNameRu = "Прямая (вертикаль)";
                    return;
                case WaterDirectionMask.Left | WaterDirectionMask.Right:
                    shapeId = WaterVisualShapeId.Straight;
                    rotationZ = 90f;
                    shapeNameRu = "Прямая (горизонталь)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Right:
                    shapeId = WaterVisualShapeId.Corner;
                    shapeNameRu = "Угол (Up+Right)";
                    return;
                case WaterDirectionMask.Right | WaterDirectionMask.Down:
                    shapeId = WaterVisualShapeId.Corner;
                    rotationZ = -90f;
                    shapeNameRu = "Угол (Right+Down)";
                    return;
                case WaterDirectionMask.Down | WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.Corner;
                    rotationZ = 180f;
                    shapeNameRu = "Угол (Down+Left)";
                    return;
                case WaterDirectionMask.Left | WaterDirectionMask.Up:
                    shapeId = WaterVisualShapeId.Corner;
                    rotationZ = 90f;
                    shapeNameRu = "Угол (Left+Up)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Right | WaterDirectionMask.Down:
                    shapeId = WaterVisualShapeId.Tee;
                    shapeNameRu = "Тройник (без Left)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Right | WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.Tee;
                    rotationZ = 90f;
                    shapeNameRu = "Тройник (без Down)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Down | WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.Tee;
                    rotationZ = 180f;
                    shapeNameRu = "Тройник (без Right)";
                    return;
                case WaterDirectionMask.Right | WaterDirectionMask.Down | WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.Tee;
                    rotationZ = -90f;
                    shapeNameRu = "Тройник (без Up)";
                    return;
                case WaterDirectionMask.Up | WaterDirectionMask.Right | WaterDirectionMask.Down | WaterDirectionMask.Left:
                    shapeId = WaterVisualShapeId.Cross;
                    shapeNameRu = "Крест";
                    return;
                default:
                    shapeId = WaterVisualShapeId.End;
                    shapeNameRu = "Конец (fallback)";
                    return;
            }
        }
    }
}
