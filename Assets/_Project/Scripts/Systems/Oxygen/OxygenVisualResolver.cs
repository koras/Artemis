namespace _Project.Scripts.Systems.Oxygen
{
    public enum OxygenVisualShapeId : byte
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
    public static class OxygenVisualResolver
    {
        /// <summary>
        /// Возвращает стабильный ключ формы кабеля для маппинга на спрайт.
        /// </summary>
        public static string ResolveShapeKey(byte OxygenMask4)
        {
            OxygenDirectionMask mask = (OxygenDirectionMask)OxygenMask4;
            return mask switch
            {
                OxygenDirectionMask.Up | OxygenDirectionMask.Down => "Vertical",
                OxygenDirectionMask.Left | OxygenDirectionMask.Right => "Horizontal",
                OxygenDirectionMask.Up | OxygenDirectionMask.Right => "CornerUpRight",
                OxygenDirectionMask.Right | OxygenDirectionMask.Down => "CornerRightDown",
                OxygenDirectionMask.Down | OxygenDirectionMask.Left => "CornerDownLeft",
                OxygenDirectionMask.Left | OxygenDirectionMask.Up => "CornerLeftUp",
                OxygenDirectionMask.Up | OxygenDirectionMask.Left | OxygenDirectionMask.Right => "TNoDown",
                OxygenDirectionMask.Right | OxygenDirectionMask.Down | OxygenDirectionMask.Left => "TNoUp",
                OxygenDirectionMask.Down | OxygenDirectionMask.Left | OxygenDirectionMask.Up => "TNoRight",
                OxygenDirectionMask.Up | OxygenDirectionMask.Down | OxygenDirectionMask.Right => "TNoLeft",
                OxygenDirectionMask.Up | OxygenDirectionMask.Right | OxygenDirectionMask.Down | OxygenDirectionMask.Left => "Cross",
                OxygenDirectionMask.Up => "EndUp",
                OxygenDirectionMask.Right => "EndRight",
                OxygenDirectionMask.Down => "EndDown",
                OxygenDirectionMask.Left => "EndLeft",
                _ => "Single"
            };
        }

        /// <summary>
        /// Возвращает форму, угол поворота и русское имя для отладки visual-тайла кабеля.
        /// </summary>
        public static void ResolveVisualDebug(byte OxygenMask4, out OxygenVisualShapeId shapeId, out float rotationZ, out string shapeNameRu)
        {
            OxygenDirectionMask mask = (OxygenDirectionMask)OxygenMask4;
            rotationZ = 0f;

            switch (mask)
            {
                case 0:
                    shapeId = OxygenVisualShapeId.Single;
                    shapeNameRu = "Одиночный (без соседей)";
                    return;
                case OxygenDirectionMask.Up:
                    shapeId = OxygenVisualShapeId.End;
                    shapeNameRu = "Конец (вверх)";
                    return;
                case OxygenDirectionMask.Right:
                    shapeId = OxygenVisualShapeId.End;
                    rotationZ = -90f;
                    shapeNameRu = "Конец (вправо)";
                    return;
                case OxygenDirectionMask.Down:
                    shapeId = OxygenVisualShapeId.End;
                    rotationZ = 180f;
                    shapeNameRu = "Конец (вниз)";
                    return;
                case OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.End;
                    rotationZ = 90f;
                    shapeNameRu = "Конец (влево)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Down:
                    shapeId = OxygenVisualShapeId.Straight;
                    shapeNameRu = "Прямая (вертикаль)";
                    return;
                case OxygenDirectionMask.Left | OxygenDirectionMask.Right:
                    shapeId = OxygenVisualShapeId.Straight;
                    rotationZ = 90f;
                    shapeNameRu = "Прямая (горизонталь)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Right:
                    shapeId = OxygenVisualShapeId.Corner;
                    shapeNameRu = "Угол (Up+Right)";
                    return;
                case OxygenDirectionMask.Right | OxygenDirectionMask.Down:
                    shapeId = OxygenVisualShapeId.Corner;
                    rotationZ = -90f;
                    shapeNameRu = "Угол (Right+Down)";
                    return;
                case OxygenDirectionMask.Down | OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.Corner;
                    rotationZ = 180f;
                    shapeNameRu = "Угол (Down+Left)";
                    return;
                case OxygenDirectionMask.Left | OxygenDirectionMask.Up:
                    shapeId = OxygenVisualShapeId.Corner;
                    rotationZ = 90f;
                    shapeNameRu = "Угол (Left+Up)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Right | OxygenDirectionMask.Down:
                    shapeId = OxygenVisualShapeId.Tee;
                    shapeNameRu = "Тройник (без Left)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Right | OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.Tee;
                    rotationZ = 90f;
                    shapeNameRu = "Тройник (без Down)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Down | OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.Tee;
                    rotationZ = 180f;
                    shapeNameRu = "Тройник (без Right)";
                    return;
                case OxygenDirectionMask.Right | OxygenDirectionMask.Down | OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.Tee;
                    rotationZ = -90f;
                    shapeNameRu = "Тройник (без Up)";
                    return;
                case OxygenDirectionMask.Up | OxygenDirectionMask.Right | OxygenDirectionMask.Down | OxygenDirectionMask.Left:
                    shapeId = OxygenVisualShapeId.Cross;
                    shapeNameRu = "Крест";
                    return;
                default:
                    shapeId = OxygenVisualShapeId.End;
                    shapeNameRu = "Конец (fallback)";
                    return;
            }
        }
    }
}
