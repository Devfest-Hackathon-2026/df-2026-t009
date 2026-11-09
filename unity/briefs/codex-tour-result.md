Implemented the tour, captions/end card, retakes, input blocking, and debug-log cleanup.

- Added `App/DemoTour.cs` and `App/Ui/Caption.cs`; updated app/HUD/camera code and both identical USS files.
- Start with `?tour=1`; use `?tour=1&start=4` for retakes, steps 1–7.
- Guestbook text is previewed without sending; events are shown without joining.
- Editor/WebGL API compilation and stylesheet checks passed.

Unity was not launched, so playback and recording remain unverified. Network waits or distant fallback drops can extend the approximate 90-second duration.