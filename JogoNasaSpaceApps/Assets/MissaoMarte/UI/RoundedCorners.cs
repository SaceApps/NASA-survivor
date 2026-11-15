using UnityEngine;
using UnityEngine.UI;

namespace MissaoMarte.UI
{
    /// <summary>
    /// Mantém o raio dos cantos de um painel arredondado (sprite fatiado de <see cref="UiBuilder"/>).
    /// O raio é limitado à metade do lado menor: assim uma peça com raio grande vira pílula, e não elipse.
    /// Recalcula quando o tamanho muda (troca de resolução, barras que enchem e esvaziam).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class RoundedCorners : MonoBehaviour
    {
        /// <summary>Raio desejado, em unidades da tela de referência.</summary>
        public float Radius = UiBuilder.DefaultRadius;

        Image image;

        void OnEnable()
        {
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        public void Apply()
        {
            if (image == null)
                image = GetComponent<Image>();
            if (image == null)
                return;

            Rect rect = ((RectTransform)transform).rect;
            float limit = Mathf.Min(rect.width, rect.height) / 2f;
            float radius = limit > 0f ? Mathf.Min(Radius, limit) : Radius;
            float multiplier = UiBuilder.SpriteCorner / Mathf.Max(radius, 0.01f);
            if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
                image.pixelsPerUnitMultiplier = multiplier;
        }
    }
}
