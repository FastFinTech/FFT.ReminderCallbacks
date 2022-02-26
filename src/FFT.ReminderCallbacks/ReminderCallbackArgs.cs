// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.ReminderCallbacks;

/// <summary>
/// Use the fields in this class to check for late callbacks,
/// possibly as a result of the system recovering from suspension or hibernation.
/// </summary>
public sealed class ReminderCallbackArgs
{
  internal ReminderCallbackArgs(ReminderCallback sender, TimeStamp actualCallbackTime)
  {
    Sender = sender;
    ActualCallbackTime = actualCallbackTime;
  }

  /// <summary>
  /// The reminder object raising this reminder.
  /// </summary>
  public ReminderCallback Sender { get; }

  /// <summary>
  /// The name of the reminder-raising object.
  /// </summary>
  public string ReminderName => Sender.ReminderName;

  /// <summary>
  /// The time that the reminder was supposed to be raised.
  /// </summary>
  public TimeStamp RequestedCallbackTime => Sender.ReminderTime;

  /// <summary>
  /// The actual time that the event was raised.
  /// It can be late due to clock inaccuracies, cpu load, or due to the
  /// operating system being asleep (hibernated) at the time of the event.
  /// </summary>
  public TimeStamp ActualCallbackTime { get; }
}
