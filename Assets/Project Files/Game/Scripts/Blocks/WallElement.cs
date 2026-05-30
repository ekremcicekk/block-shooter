using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Marker component placed on empty grid-cell GameObjects.
    /// Provides no gameplay logic — identifies the object as a non-interactive grid placeholder.
    /// </summary>
    public class WallElement : MonoBehaviour
    {
        [SerializeField] private int _gridColumn;
        [SerializeField] private int _gridRow;

        public int GridColumn => _gridColumn;
        public int GridRow    => _gridRow;

        public void SetGridPosition(int col, int row)
        {
            _gridColumn = col;
            _gridRow    = row;
        }
    }
}
