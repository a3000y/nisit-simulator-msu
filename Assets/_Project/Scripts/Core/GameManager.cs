using System;
using UnityEngine;

namespace NisitSimulator.Core
{
    // ตัวกลางคุมสถานะเกมทั้งหมด (Singleton)
    // ทุกระบบเรียกใช้ผ่าน GameManager.Instance และฟัง event OnStateChanged
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // สถานะปัจจุบันของเกม
        public GameState State { get; private set; } = GameState.MainMenu;

        // สาเหตุที่เกมจบล่าสุด (ตาย/ตก/จบการศึกษา)
        public EndReason LastEndReason { get; private set; } = EndReason.None;

        // event ยิงเมื่อสถานะเปลี่ยน (UI/ระบบอื่นมา subscribe)
        public event Action<GameState> OnStateChanged;

        void Awake()
        {
            // บังคับให้มีตัวเดียวในเกม และอยู่ข้ามฉาก
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // หมายเหตุ: ยังไม่ใช้ DontDestroyOnLoad เพราะตอนนี้เป็นซีนเดียว
            // ทำให้ปุ่ม Restart (โหลดซีนใหม่) รีเซ็ตสถานะสะอาด — M4 จะใส่ persistence ตอนมีหลายซีน
        }

        // เปลี่ยนสถานะเกม + แจ้งทุกคนที่ฟังอยู่
        public void SetState(GameState newState)
        {
            if (State == newState) return;
            State = newState;

            // หยุด/เดินเวลาเกมตามสถานะ (Pause = หยุดฟิสิกส์)
            Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

            OnStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] เปลี่ยนสถานะเป็น: {newState}");
        }

        // ตัวช่วยเรียกสั้นๆ จากปุ่ม UI
        public void StartGame() => SetState(GameState.Playing);
        public void PauseGame() => SetState(GameState.Paused);
        public void ResumeGame() => SetState(GameState.Playing);

        public bool IsPlaying => State == GameState.Playing;

        // เกมยัง "เดินอยู่" ไหม (ไม่หยุด/ไม่จบ) — ระบบต่างๆ ใช้เช็คก่อนทำงาน
        public bool IsActive => State != GameState.Paused
                             && State != GameState.GameOver
                             && State != GameState.Win;

        // จบเกมพร้อมเหตุผล → เลือกสถานะ Win/GameOver ให้อัตโนมัติ
        public void EndGame(EndReason reason)
        {
            if (State == GameState.GameOver || State == GameState.Win) return; // จบไปแล้ว
            LastEndReason = reason;
            SetState(reason == EndReason.Graduated ? GameState.Win : GameState.GameOver);
        }
    }
}
