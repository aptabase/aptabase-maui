## Unreleased

* Requeue a failing event or error at the tail of the persistent queue instead of unwinding the read loop, so a single undeliverable item can no longer block everything queued behind it
* Back off between attempts while sends are failing (30s doubling to a 5 minute cap) instead of retrying every 30s, with jitter so clients do not retry a recovering server in lockstep
* Observe the shutdown token while backing off, so disposing the client no longer waits out a full retry delay

## 0.2.0

* Add `TrackError(Exception, fatal)` to `IAptabaseClient` for reporting handled errors and crashes
* Crash reporter now sends structured error reports (error type, message, stack trace, severity, kind) instead of synthesized events
* Persist error reports on disk when `EnablePersistence` is set, so fatal crashes are delivered on the next app launch
* Include `isDebug` in error reports so debug-build errors can be separated from production data server-side
* Trust the local dev certificate on Mac Catalyst when targeting a local Aptabase instance

## 0.1.0

* Add `EnablePersistence` to persist events on disk before sending them to the server
* Add `EnableCrashReporting` to log application crashes, unhandled exceptions

## 0.0.9

* Fix net8 compatibility issues
* Use `System.Threading.Channels` to send events asynchronously
* Add `IsDebugMode` to `AptabaseOptions` (fixes Android detection + avoids reflection if specified by the consumer of the sdk)
* Add `DeviceModel` to system properties

## 0.0.8

* Use new session id format

## 0.0.7

* Use a more reliable method for Debug/Release mode detection
* Update docs

## 0.0.6

* Fix automatic Debug/Release mode detection

## 0.0.5

* Added support for automatic segregation of Debug/Release events
* Explicit nullable types