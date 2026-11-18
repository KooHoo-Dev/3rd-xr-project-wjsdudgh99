using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 모기 한 마리 (기획서 2.3 FSM).
    ///  Fly      : 불 꺼짐. 머리 주변(손 닿는 범위 바깥쪽)을 랜덤 비행하며 "애앵~"
    ///  Approach : 불 꺼짐, 가끔. 몇 초간 귓가를 맴돌다 Fly 로 (맨손 잡기 기회)
    ///  ToWall   : 불 켜짐. 3초 더 날다가 가까운 착지 지점으로
    ///  Landed   : 착지. 소리 멈춤. 가까이서(0.6m) 손/파리채가 빠르게(1.0m/s) 움직이면 Alerted
    ///  Alerted  : 0.25초 날개를 파르르 떤 뒤 Escape (그 사이에 치면 잡힘)
    ///  Escape   : 날아올라 사라짐 → Hidden, "모기가 사라졌습니다"
    ///  Hidden   : 불이 켜져 있는 동안 숨어 있음. 불 끄고 5초 뒤 멀리서 소리가 커지며 Fly 로 재등장
    ///  Caught   : 잡힘. 납작해진 채 잠시 남았다가 사라짐
    /// 잡기 판정(거리 구간)은 TryCatch 에서 함. 수치는 MosquitoSwarm 에 모아 둠.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Mosquito : MonoBehaviour
    {
        public enum State { Fly, Approach, ToWall, Landed, Alerted, Escape, Hidden, Caught }
        public enum Method { Hand, Swatter }

        public State Current { get; private set; } = State.Hidden;
        public bool IsCatchable => Current != State.Hidden && Current != State.Caught && Current != State.Escape;
        public bool IsCaught => Current == State.Caught;

        [HideInInspector] public MosquitoSwarm swarm;

        AudioSource _audio;
        MosquitoWingFlap _wings;
        Renderer[] _renderers;
        Collider[] _colliders;
        Vector3 _baseScale;

        Vector3 _velocity;
        Vector3 _waypoint;
        float _stateTime;
        float _nextWaypoint;
        float _nextApproachRoll;
        float _approachAngle;
        float _noiseSeed;
        bool _dancing;
        float _pitchJitter = 1f;
        float _volumeFade = 1f;
        LandingPoint _landing;
        Vector3 _escapeDir;

        MosquitoSwarm.Settings S => swarm.settings;

        void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _wings = GetComponent<MosquitoWingFlap>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _baseScale = transform.localScale;
            _noiseSeed = Random.value * 100f;
            _pitchJitter = Random.Range(0.93f, 1.07f); // 마리마다 소리 높이가 조금씩 다르게
        }

        // ------------------------------------------------------------------
        // 바깥에서 부르는 것
        // ------------------------------------------------------------------

        /// <summary>판 시작: pos 에서 소리가 서서히 커지며 날기 시작</summary>
        public void Spawn(Vector3 pos)
        {
            transform.localScale = _baseScale;
            transform.position = pos;
            _velocity = Vector3.zero;
            SetVisible(true);
            _volumeFade = 0f; // 멀리서부터 점점 커지는 "애앵~"
            if (_audio.clip != null && !_audio.isPlaying) _audio.Play();
            Enter(State.Fly);
        }

        /// <summary>모기와 댄싱 파티: 못 잡은 모기가 모두 나와서 머리 주변을 신나게 맴돎 (다음 판까지)</summary>
        public void Dance()
        {
            if (Current == State.Caught) return;
            if (Current == State.Hidden) Spawn(swarm.RandomFlyPoint());
            _dancing = true;
            Enter(State.Approach);
        }

        /// <summary>판 끝 / 정리: 소리와 모습을 끔</summary>
        public void Despawn()
        {
            _dancing = false;
            ReleaseLanding();
            _audio.Stop();
            SetVisible(false);
            Current = State.Hidden;
            enabled = false;
        }

        /// <summary>
        /// 잡기 시도. 11/18 결정으로 맨손 잡기는 뺌(settings.handCatch = false) → 파리채로만, 거리 제한 없음.
        /// (맨손을 다시 켜면: 근거리 nearZone 안은 맨손만, handReach 밖은 파리채만)
        /// </summary>
        public bool TryCatch(Method method)
        {
            if (!IsCatchable) return false;
            float d = Vector3.Distance(transform.position, swarm.HeadPosition);
            if (method == Method.Swatter && d < S.nearZone)
            {
                swarm.Message(MosquitoSwarm.Msg.TooClose);
                return false;
            }
            if (method == Method.Hand && (!S.handCatch || d > S.handReach)) return false;

            Enter(State.Caught);
            swarm.NotifyCaught(this, method);
            return true;
        }

        // ------------------------------------------------------------------
        // 상태
        // ------------------------------------------------------------------

        void Enter(State s)
        {
            var prevLanding = _landing;
            if (Current == State.Landed || Current == State.Alerted || Current == State.ToWall)
                if (s != State.Landed && s != State.Alerted) ReleaseLanding();

            Current = s;
            _stateTime = 0f;

            switch (s)
            {
                case State.Fly:
                    SetWings(true);
                    _nextWaypoint = 0f;
                    _nextApproachRoll = Random.Range(S.approachEvery.x, S.approachEvery.y);
                    break;
                case State.Approach:
                    SetWings(true);
                    _approachAngle = Random.value * 360f;
                    break;
                case State.ToWall:
                    SetWings(true);
                    break;
                case State.Landed:
                    SetWings(false);
                    break;
                case State.Alerted:
                    if (_wings != null) _wings.shiver = true;
                    break;
                case State.Escape:
                    if (_wings != null) _wings.shiver = false;
                    SetWings(true);
                    _escapeDir = ((prevLanding != null ? prevLanding.Normal : Vector3.up) + Vector3.up * 1.5f).normalized;
                    swarm.Message(MosquitoSwarm.Msg.Escaped);
                    break;
                case State.Hidden:
                    SetVisible(false);
                    break;
                case State.Caught:
                    if (_wings != null) _wings.shiver = false;
                    ReleaseLanding();
                    SetWings(false);
                    foreach (var c in _colliders) c.enabled = false;
                    _audio.Stop();
                    // 납작하게 (등 방향으로 눌림)
                    transform.localScale = Vector3.Scale(_baseScale, new Vector3(1.3f, 0.25f, 1.1f));
                    break;
            }
        }

        void Update()
        {
            if (swarm == null) return;
            float dt = Time.deltaTime;
            _stateTime += dt;
            bool lightOn = swarm.LightOn;

            switch (Current)
            {
                case State.Fly:
                    if (lightOn) { Enter(State.ToWall); break; }
                    FlyAround(dt);
                    _nextApproachRoll -= dt;
                    if (_nextApproachRoll <= 0f) Enter(State.Approach);
                    break;

                case State.Approach:
                    if (_dancing) { Orbit(dt * 1.8f); break; } // 댄싱 파티: 불과 상관없이 계속 빠르게 맴돎
                    if (lightOn) { Enter(State.ToWall); break; }
                    Orbit(dt);
                    if (_stateTime > S.approachSeconds) Enter(State.Fly);
                    break;

                case State.ToWall:
                    if (!lightOn) { Enter(State.Fly); break; }
                    if (_stateTime < S.toWallDelay) { FlyAround(dt); break; }
                    if (_landing == null)
                    {
                        _landing = swarm.ReserveLanding(this, transform.position);
                        if (_landing == null) { FlyAround(dt); break; } // 빈자리 없음 → 계속 날기
                    }
                    if (FlyTo(_landing.transform.position + _landing.Normal * S.landOffset, S.flySpeed, dt, 0.02f))
                    {
                        transform.SetPositionAndRotation(_landing.transform.position + _landing.Normal * S.landOffset, LandedRotation(_landing));
                        Enter(State.Landed);
                    }
                    break;

                case State.Landed:
                    if (!lightOn) { Enter(State.Fly); break; }
                    if (_stateTime > 0.3f && SeesThreat()) Enter(State.Alerted);
                    break;

                case State.Alerted:
                    if (_stateTime >= S.alertDelay) Enter(State.Escape);
                    break;

                case State.Escape:
                    _velocity = _escapeDir * S.escapeSpeed;
                    transform.position += _velocity * dt;
                    if (_stateTime > S.escapeSeconds) Enter(State.Hidden);
                    break;

                case State.Hidden:
                    // 불이 꺼진 지 5초가 지나면 멀리서 다시 등장
                    if (!lightOn && swarm.LightOffSeconds >= S.respawnAfterDark)
                        Spawn(swarm.RandomFarPoint());
                    break;

                case State.Caught:
                    if (_stateTime > S.caughtLinger)
                    {
                        SetVisible(false);
                        enabled = false;
                    }
                    break;
            }

            UpdateAudio(dt);
        }

        // ------------------------------------------------------------------
        // 비행
        // ------------------------------------------------------------------

        void FlyAround(float dt)
        {
            if (Time.time >= _nextWaypoint || (transform.position - _waypoint).sqrMagnitude < 0.01f)
            {
                _waypoint = swarm.RandomFlyPoint();
                _nextWaypoint = Time.time + Random.Range(1.2f, 2.5f);
            }
            FlyTo(_waypoint, S.flySpeed, dt, 0f);
        }

        void Orbit(float dt)
        {
            // 귀 높이에서 머리 주변을 빙빙 (반지름이 살짝 출렁임)
            _approachAngle += S.approachOrbitSpeed * dt;
            float r = S.approachRadius + 0.05f * Mathf.Sin(_stateTime * 2.1f);
            var head = swarm.HeadPosition;
            var target = head + Quaternion.Euler(0f, _approachAngle, 0f) * Vector3.forward * r
                              + Vector3.up * (0.05f * Mathf.Sin(_stateTime * 3.3f) - 0.03f);
            FlyTo(target, S.flySpeed * 1.2f, dt, 0f);
        }

        /// <summary>target 쪽으로 날기 (부드럽게 방향 전환 + 흔들림). 도착하면 true</summary>
        bool FlyTo(Vector3 target, float speed, float dt, float arriveDist)
        {
            var to = target - transform.position;
            float dist = to.magnitude;
            if (arriveDist > 0f && dist <= arriveDist) return true;

            // 착지할 때는 가까워질수록 느려짐
            float s = arriveDist > 0f ? Mathf.Min(speed, Mathf.Max(0.15f, dist * 3f)) : speed;
            var desired = dist > 1e-4f ? to / dist * s : Vector3.zero;
            _velocity = Vector3.MoveTowards(_velocity, desired, S.acceleration * dt);

            // 모기다운 불규칙한 흔들림
            float t = Time.time * 3f + _noiseSeed;
            var wobble = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f);
            float wobbleScale = arriveDist > 0f ? Mathf.Clamp01(dist * 4f) : 1f;
            transform.position += (_velocity + wobble * S.wobble * wobbleScale) * dt;

            if (_velocity.sqrMagnitude > 1e-4f)
            {
                var flat = Vector3.ProjectOnPlane(_velocity, Vector3.up);
                var look = flat.sqrMagnitude > 1e-4f ? flat : _velocity;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), 8f * dt);
            }
            return arriveDist > 0f && (target - transform.position).magnitude <= arriveDist;
        }

        static Quaternion LandedRotation(LandingPoint p)
        {
            // 등(+Y) = 표면 바깥, 머리(+Z) = 표면을 따라 위쪽 (바닥/이불 위면 랜덤 방향)
            var n = p.Normal;
            var fwd = Vector3.ProjectOnPlane(Vector3.up, n);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
            return Quaternion.LookRotation(fwd.normalized, n);
        }

        // ------------------------------------------------------------------
        // 눈치 / 소리 / 기타
        // ------------------------------------------------------------------

        bool SeesThreat()
        {
            var p = transform.position;
            foreach (var t in VelocityTracker.All)
            {
                if (!t.alertsMosquitoes) continue;
                if ((t.Position - p).sqrMagnitude > S.alertRadius * S.alertRadius) continue;
                if (t.Speed >= S.alertSpeed) return true;
            }
            return false;
        }

        void UpdateAudio(float dt)
        {
            if (_audio == null) return;
            bool buzzing = Current == State.Fly || Current == State.Approach || Current == State.ToWall || Current == State.Escape;
            float target = buzzing ? 1f : 0f;
            // 재등장 때는 천천히(2초) 커지고, 착지하면 빠르게 멈춤
            float rate = buzzing ? 0.5f : 4f;
            _volumeFade = Mathf.MoveTowards(_volumeFade, target, rate * dt);
            _audio.volume = S.buzzVolume * _volumeFade;

            // 머리에 가까울수록 음이 조금 높아짐 (접근 긴장감)
            float d = Vector3.Distance(transform.position, swarm.HeadPosition);
            _audio.pitch = Mathf.Lerp(1.15f, 0.95f, Mathf.InverseLerp(0.2f, 1.2f, d)) * _pitchJitter;
        }

        void SetWings(bool flap)
        {
            if (_wings != null) _wings.flapping = flap;
        }

        void SetVisible(bool v)
        {
            foreach (var r in _renderers) r.enabled = v;
            foreach (var c in _colliders) c.enabled = v;
            enabled = true;
        }

        void ReleaseLanding()
        {
            if (_landing != null && _landing.occupant == this) _landing.occupant = null;
            _landing = null;
        }

        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            ReleaseLanding();
        }

        // 오디오 출력이 새로 만들어지면(GameManager 가 포커스 복구 때 Reset) 모든 소리가 멈추므로 다시 재생
        void OnAudioConfigurationChanged(bool deviceWasChanged) => ResumeAudio();

        // Meta 메뉴(녹화 시작 등)를 열었다 닫으면 앱이 포커스를 잃었다 되찾음.
        // 11/18 헤드셋 테스트에서 그 뒤로 모기 소리가 안 났음 → 포커스를 되찾을 때 소리를 다시 틀어 줌
        void OnApplicationFocus(bool focus)
        {
            if (focus) ResumeAudio();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) ResumeAudio();
        }

        void ResumeAudio()
        {
            if (_audio == null || _audio.clip == null || !enabled || Current == State.Caught) return;
            if (!_audio.isPlaying || _audio.time <= 0f) { _audio.Stop(); _audio.Play(); }
        }
    }
}
