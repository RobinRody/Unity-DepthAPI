using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace MultiPlayer
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class JankenWinCountDisplay : MonoBehaviour
    {
        private TextMeshProUGUI textComponent;
        public ChapterManager chapterManager;

        void Start()
        {
            // 取得 TextMeshProUGUI 組件
            textComponent = GetComponent<TextMeshProUGUI>();

            // 尋找場景中的 ChapterManager
            if (chapterManager == null)
                chapterManager = FindObjectOfType<ChapterManager>();

            if (chapterManager == null)
            {
                Debug.LogError("<color=red>[JankenWinCountDisplay]</color> ChapterManager not found in scene!");
            }

            if (textComponent == null)
            {
                Debug.LogError("<color=red>[JankenWinCountDisplay]</color> TextMeshProUGUI component not found!");
            }
        }

        void Update()
        {
            if (chapterManager != null && textComponent != null)
            {
                // 更新文字內容為 "JankenWin/JankenRound"
                textComponent.text = $"{chapterManager.JankenWin}/{chapterManager.JankenRound}";
            }
        }
    }
}