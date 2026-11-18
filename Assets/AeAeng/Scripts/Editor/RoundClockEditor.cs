using UnityEditor;
using UnityEngine;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// RoundClock 인스펙터에 "시간 이동" 도구를 붙인다 (플레이 모드에서만).
    /// 게임 시각 슬라이더 + 자주 쓰는 시각 버튼으로 해 뜨기, 판 종료 등을 바로 확인.
    /// </summary>
    [CustomEditor(typeof(RoundClock))]
    public class RoundClockEditor : UnityEditor.Editor
    {
        static readonly (string label, float hour)[] Presets =
        {
            ("1:00", 1f), ("3:00", 3f), ("5:00", 5f), ("5:30 해 뜨기", 5.5f), ("6:30", 6.5f), ("6:55", 55f / 60f + 6f), ("7:00 끝", 7f),
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var clock = (RoundClock)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("시간 이동 (테스트)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 게임 시각을 바로 옮길 수 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("지금", $"{clock.GameTime:h:mm}  (남은 {clock.Remaining:0}초, {(clock.IsRunning ? "진행 중" : "멈춤")})");

            EditorGUI.BeginChangeCheck();
            float hour = EditorGUILayout.Slider("게임 시각", clock.GameHour, clock.startHour, clock.endHour);
            if (EditorGUI.EndChangeCheck()) clock.JumpToHour(hour);

            int perRow = 4;
            for (int i = 0; i < Presets.Length; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, Presets.Length); j++)
                    if (GUILayout.Button(Presets[j].label)) clock.JumpToHour(Presets[j].hour);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("새 판 시작 (1:00)")) clock.StartRound();

            Repaint(); // 시계가 흐르는 동안 표시 갱신
        }
    }
}
