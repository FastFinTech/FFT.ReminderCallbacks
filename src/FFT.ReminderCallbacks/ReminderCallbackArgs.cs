// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.ReminderCallbacks;

using FFT.IgnoreTasks;
using Task = System.Threading.Tasks.Task;

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

/// <summary>
/// Use the fields in this class to check for late callbacks, possibly as a result of the system recovering from suspension or hibernation.
/// </summary>
public sealed class ReminderCallbackArgs
{
  internal ReminderCallbackArgs(IReminderCallback sender, TimeStamp actualCallbackTime)
  {
    Sender = sender;
    ActualCallbackTime = actualCallbackTime;
  }

  /// <summary>
  /// The reminder object raising this reminder.
  /// </summary>
  public IReminderCallback Sender { get; }

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
  /// It can be late due to clock inaccuracies, cpu load, or due to the operating system being asleep (hibernated) 
  /// at the time of the event.
  /// </summary>
  public TimeStamp ActualCallbackTime { get; }
}

/// <inheritdoc />
/// <remarks>
/// Note this class self-disposes when it is garbage collected, so there is no guarantee that it will
/// complete its callback if you drop references to it. In fact, its awaited task may result in <see cref="OperationCanceledException"/>
/// when it is disposed due to the garbage collector activating its finalizer.
/// </remarks>
public sealed class ReminderAt : IReminderCallback
{
  private readonly Action<ReminderCallbackArgs> _callback;
  private readonly CancellationTokenSource _disposed;
  private readonly CancellationToken _disposedToken;
  private readonly TaskCompletionSource<ReminderCallbackArgs> _taskCompletionSource;

  private long _startedFlag = 0;
  private long _disposedFlag = 0;

  /// <inheritdoc />
  public string ReminderName { get; }

  /// <inheritdoc />
  public TimeStamp ReminderTime { get; }

  public ReminderAt(string reminderName, TimeStamp callbackTime, Action<ReminderCallbackArgs> callback)
  {
    ReminderName = reminderName;
    ReminderTime = callbackTime;
    _callback = callback;

    // We extract the token now so that we don't get ObjectDisposedExceptions calling Disposed.Token after we have been disposed.
    _disposed = new CancellationTokenSource();
    _disposedToken = _disposed.Token;

    _taskCompletionSource = new TaskCompletionSource<ReminderCallbackArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
  }

  /// <inheritdoc />
  public Task<ReminderCallbackArgs> Task
      => _taskCompletionSource.Task;

  ~ReminderAt()
  {
    Dispose();
  }

  /// <inheritdoc />
  public void StartMonitoring()
  {
    if (Interlocked.CompareExchange(ref _startedFlag, 1, 0) == 1) throw new InvalidOperationException("Cannot be started more than once.");
    System.Threading.Tasks.Task.Run(Work).Ignore();
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1) return;
    _disposed.Cancel();
    _disposed.Dispose();
    _taskCompletionSource.TrySetCanceled();
    GC.SuppressFinalize(this);
  }

  private async Task Work()
  {
    // Task.Delay uses System.Timers.Timer internally.
    // The best timer accuracy still has errors of +/-15ms or so.
    // When the timer delay is more than 50ms, the timer accuracy becomes very inaccurate.
    // Care is taken to make sure the event is fired at or AFTER the given time, and a series of decreasing waits is used
    // to ensure accuracy of the eventual signal.
    try
    {
      while (true)
      {
        var now = TimeStamp.Now;
        var timeUntilCallback = ReminderTime.Subtract(now);

        // Fire the callback immediately if necessary, making sure the current time is
        // definitely AFTER, not slightly before, the requested callback time.
        if (timeUntilCallback <= TimeSpan.Zero)
        {
          var args = new ReminderCallbackArgs(this, now);
          try { _callback.Invoke(args); } catch { }
          _taskCompletionSource.SetResult(args);

          // Automatically self-dispose after firing the event according to spec
          // Note that code has continued to this point without waiting for the above two operations to complete.
          Dispose();

          // And get the hell outta here!
          return;
        }

        // The wait time on Task.Delay is "wait time while the computer is alive".
        // So if you set it for one minute but suspend the operating system for 5 minutes, it will trigger at the 6 minute mark.
        // We want to minimize that behaviour by triggering immediately after the computer wakes up from hibernation, even when it's late.
        // By setting the maximum checking interval at once second, we can "immediately" (more or less) trigger a late event after the computer wakes up from hibernation.
        if (timeUntilCallback.TotalSeconds > 1)
          timeUntilCallback = TimeSpan.FromSeconds(1);

        // Thow an <see cref="OperationCanceledException"/> if we are disposed before the callback time arrives.
        await System.Threading.Tasks.Task.Delay(timeUntilCallback, _disposedToken).ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException)
    { // Happens when disposed. Just eat it.
    }
    catch (Exception x)
    {
      // This should NEVER happen.
      // TODO: Figure out a way to report this to the user.
    }
  }
}
