using TMPro;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 왼손 손목 UI (기획서 4.2): 이번 판에 잡은 모기 수 ( 2 / 3 ) + 지금까지의 파티 횟수.
    /// 왼손 손등 쪽 손목에 시계처럼 붙어 있어서, 손목을 돌려 보면 읽힌다.
    /// </summary>
    public class WristUI : MonoBehaviour
    {
        public TextMeshPro text;
        public MosquitoSwarm swarm;

        int _caught = -1, _total = -1, _dream = -1, _dance = -1;

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TextMeshPro>(true);
            if (swarm == null) swarm = FindFirstObjectByType<MosquitoSwarm>();
        }

        void Update()
        {
            if (text == null) return;
            int caught = swarm != null ? swarm.Caught : 0;
            int total = swarm != null ? swarm.Total : 0;
            var gm = GameManager.Instance;
            int dream = gm != null ? gm.DreamParties : 0;
            int dance = gm != null ? gm.DanceParties : 0;
            if (caught == _caught && total == _total && dream == _dream && dance == _dance) return;

            _caught = caught; _total = total; _dream = dream; _dance = dance;
            text.text = $"<size=140%>모기 {caught} / {total}</size>\n<size=80%>꿈나라 {dream} · 댄싱 {dance}</size>";
        }
    }
}
