namespace _Project.Scripts.Systems.Power
{
    public enum CableVisualShapeId : byte
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
    public static class CableVisualResolver
    {
        /// <summary>
        /// Возвращает стабильный ключ формы кабеля для маппинга на спрайт.
        /// </summary>
        public static string ResolveShapeKey(byte cableMask4)
        {
            CableDirectionMask mask = (CableDirectionMask)cableMask4;
            return mask switch
            {
                CableDirectionMask.Up | CableDirectionMask.Down => "Vertical",
                CableDirectionMask.Left | CableDirectionMask.Right => "Horizontal",
                CableDirectionMask.Up | CableDirectionMask.Right => "CornerUpRight",
                CableDirectionMask.Right | CableDirectionMask.Down => "CornerRightDown",
                CableDirectionMask.Down | CableDirectionMask.Left => "CornerDownLeft",
                CableDirectionMask.Left | CableDirectionMask.Up => "CornerLeftUp",
                CableDirectionMask.Up | CableDirectionMask.Left | CableDirectionMask.Right => "TNoDown",
                CableDirectionMask.Right | CableDirectionMask.Down | CableDirectionMask.Left => "TNoUp",
                CableDirectionMask.Down | CableDirectionMask.Left | CableDirectionMask.Up => "TNoRight",
                CableDirectionMask.Up | CableDirectionMask.Down | CableDirectionMask.Right => "TNoLeft",
                CableDirectionMask.Up | CableDirectionMask.Right | CableDirectionMask.Down | CableDirectionMask.Left => "Cross",
                CableDirectionMask.Up => "EndUp",
                CableDirectionMask.Right => "EndRight",
                CableDirectionMask.Down => "EndDown",
                CableDirectionMask.Left => "EndLeft",
                _ => "Single"
            };
        }

        /// <summary>
        /// Возвращает форму, угол поворота и русское имя для отладки visual-тайла кабеля.
        /// </summary>
        public static void ResolveVisualDebug(byte cableMask4, out CableVisualShapeId shapeId, out float rotationZ, out string shapeNameRu)
        {
            CableDirectionMask mask = (CableDirectionMask)cableMask4;
            rotationZ = 0f;

            switch (mask)
            {
                case 0:
                    shapeId = CableVisualShapeId.Single;
                    shapeNameRu = "Одиночный (без соседей)";
                    return;
                case CableDirectionMask.Up:
                    shapeId = CableVisualShapeId.End;
                    shapeNameRu = "Конец (вверх)";
                    return;
                case CableDirectionMask.Right:
                    shapeId = CableVisualShapeId.End;
                    rotationZ = -90f;
                    shapeNameRu = "Конец (вправо)";
                    return;
                case CableDirectionMask.Down:
                    shapeId = CableVisualShapeId.End;
                    rotationZ = 180f;
                    shapeNameRu = "Конец (вниз)";
                    return;
                case CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.End;
                    rotationZ = 90f;
                    shapeNameRu = "Конец (влево)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Down:
                    shapeId = CableVisualShapeId.Straight;
                    shapeNameRu = "Прямая (вертикаль)";
                    return;
                case CableDirectionMask.Left | CableDirectionMask.Right:
                    shapeId = CableVisualShapeId.Straight;
                    rotationZ = 90f;
                    shapeNameRu = "Прямая (горизонталь)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Right:
                    shapeId = CableVisualShapeId.Corner;
                    shapeNameRu = "Угол (Up+Right)";
                    return;
                case CableDirectionMask.Right | CableDirectionMask.Down:
                    shapeId = CableVisualShapeId.Corner;
                    rotationZ = -90f;
                    shapeNameRu = "Угол (Right+Down)";
                    return;
                case CableDirectionMask.Down | CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.Corner;
                    rotationZ = 180f;
                    shapeNameRu = "Угол (Down+Left)";
                    return;
                case CableDirectionMask.Left | CableDirectionMask.Up:
                    shapeId = CableVisualShapeId.Corner;
                    rotationZ = 90f;
                    shapeNameRu = "Угол (Left+Up)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Right | CableDirectionMask.Down:
                    shapeId = CableVisualShapeId.Tee;
                    shapeNameRu = "Тройник (без Left)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Right | CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.Tee;
                    rotationZ = 90f;
                    shapeNameRu = "Тройник (без Down)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Down | CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.Tee;
                    rotationZ = 180f;
                    shapeNameRu = "Тройник (без Right)";
                    return;
                case CableDirectionMask.Right | CableDirectionMask.Down | CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.Tee;
                    rotationZ = -90f;
                    shapeNameRu = "Тройник (без Up)";
                    return;
                case CableDirectionMask.Up | CableDirectionMask.Right | CableDirectionMask.Down | CableDirectionMask.Left:
                    shapeId = CableVisualShapeId.Cross;
                    shapeNameRu = "Крест";
                    return;
                default:
                    shapeId = CableVisualShapeId.End;
                    shapeNameRu = "Конец (fallback)";
                    return;
            }
        }
    }
}
