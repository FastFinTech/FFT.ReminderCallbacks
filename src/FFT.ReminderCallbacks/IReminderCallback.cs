// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.ReminderCallbacks;

/// <summary>
/// Ensures the callback is fired at or after the given time, never slightly before the given time.
/// Tests show that the callback is often up to 14ms late.
/// You can call the <see cref="IDisposable.Dispose"/> method to cancel the callback.
/// The IEventAt object automatically disposes itself after it makes it callback.
/// IMPORTANT!! If the operating system is suspended and comes back alive after the given time,
/// the callback will fire LATE! Your calling code needs to check for late callbacks due to the system waking up from suspension.
/// </summary>
public interface IReminderCallback : IDisposable
{
  /// <summary>
  /// The name of the reminder.
  /// </summary>
  string ReminderName { get; }

  /// <summary>
  /// The time that the event should be raised.
  /// </summary>
  TimeStamp ReminderTime { get; }

  /// <summary>
  /// A task that completes when the event has been raised.
  /// The task completes with an <see cref="OperationCanceledException"/>
  /// if the event is disposed before it is triggered.
  /// </summary>
  Task<ReminderCallbackArgs> Task { get; }

  /// <summary>
  /// Call this to initiate monitoring.
  /// The reminder won't be raised unless this method has been called.
  /// </summary>
  void StartMonitoring();
}
