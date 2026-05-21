using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    // Displays one obstacle type in the goal panel: icon + remaining count.
    // When count reaches 0 the count text is swapped for a goal_check checkmark.
    public class GoalEntryUI : MonoBehaviour
    {
        [SerializeField] Image           _icon;
        [SerializeField] TextMeshProUGUI _countText;
        [SerializeField] Image           _checkmark; // goal_check.png, inactive by default

        public void Setup(Sprite iconSprite, int count)
        {
            _icon.sprite = iconSprite;
            _countText.text = count.ToString();
            _countText.gameObject.SetActive(true);
            _checkmark.gameObject.SetActive(false);
        }

        public void UpdateCount(int count)
        {
            if (count <= 0)
            {
                _countText.gameObject.SetActive(false);
                _checkmark.gameObject.SetActive(true);
                _checkmark.transform.localScale = Vector3.zero;
                _checkmark.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                _countText.text = count.ToString();
            }
        }
    }
}
