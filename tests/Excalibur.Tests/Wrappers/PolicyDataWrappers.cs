using System.Reflection;

namespace Excalibur.Tests.Wrappers;

/// <summary>
///     Wrapper for the internal Activities class.
/// </summary>
public class ActivitiesWrapper
{
	private readonly object _activitiesInstance;
	private readonly PropertyInfo _valueProperty;

	/// <summary>
	///     Initializes a new instance of the <see cref="ActivitiesWrapper" /> class.
	/// </summary>
	public ActivitiesWrapper()
		: this(Activator.CreateInstance(Type.GetType("Excalibur.A3.Authorization.PolicyData.Activities, Excalibur.A3")))
	{
	}

	public ActivitiesWrapper(object activitiesInstance)
	{
		_activitiesInstance = activitiesInstance ?? throw new ArgumentNullException(nameof(activitiesInstance));
		var type = activitiesInstance.GetType();
		_valueProperty = type.GetProperty("Value");
	}

	/// <summary>
	///     Gets the activities dictionary.
	/// </summary>
	public IDictionary<string, object> Value =>
		(IDictionary<string, object>)_valueProperty.GetValue(_activitiesInstance);

	public object InternalInstance => _activitiesInstance;

	/// <summary>
	///     Sets the value property for testing.
	/// </summary>
	/// <param name="value"> The value to set. </param>
	public void SetValue(IDictionary<string, object> value)
	{
		var fieldInfo = _activitiesInstance.GetType().GetField("_value",
			BindingFlags.NonPublic | BindingFlags.Instance);

		fieldInfo?.SetValue(_activitiesInstance, value);
	}
}

/// <summary>
///     Wrapper for the internal ActivityGroups class.
/// </summary>
public class ActivityGroupsWrapper
{
	private readonly object _activityGroupsInstance;
	private readonly MethodInfo _valueMethod;

	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityGroupsWrapper" /> class.
	/// </summary>
	public ActivityGroupsWrapper()
		: this(Activator.CreateInstance(Type.GetType("Excalibur.A3.Authorization.PolicyData.ActivityGroups, Excalibur.A3")))
	{
	}

	public ActivityGroupsWrapper(object activityGroupsInstance)
	{
		_activityGroupsInstance = activityGroupsInstance ?? throw new ArgumentNullException(nameof(activityGroupsInstance));
		_valueMethod = activityGroupsInstance.GetType().GetMethod("Value");
	}

	public object InternalInstance => _activityGroupsInstance;

	/// <summary>
	///     Gets the activity groups.
	/// </summary>
	/// <returns> A dictionary of activity groups. </returns>
	public async Task<IDictionary<string, object>> Value()
	{
		var task = (Task<IDictionary<string, object>>)_valueMethod.Invoke(_activityGroupsInstance, null);
		return await task.ConfigureAwait(false);
	}

	/// <summary>
	///     Sets the result of the Value method for testing.
	/// </summary>
	/// <param name="result"> The result to return. </param>
	public void SetValueResult(IDictionary<string, object> result)
	{
		var taskCompletionSource = new TaskCompletionSource<IDictionary<string, object>>();
		taskCompletionSource.SetResult(result);

		var fieldInfo = _activityGroupsInstance.GetType().GetField("_groups",
			BindingFlags.NonPublic | BindingFlags.Instance);

		fieldInfo?.SetValue(_activityGroupsInstance, taskCompletionSource.Task);
	}
}

/// <summary>
///     Wrapper for the internal UserGrants class.
/// </summary>
public class UserGrantsWrapper
{
	private readonly object _userGrantsInstance;
	private readonly MethodInfo _valueMethod;

	/// <summary>
	///     Initializes a new instance of the <see cref="UserGrantsWrapper" /> class.
	/// </summary>
	public UserGrantsWrapper()
		: this(Activator.CreateInstance(Type.GetType("Excalibur.A3.Authorization.PolicyData.UserGrants, Excalibur.A3")))
	{
	}

	public UserGrantsWrapper(object userGrantsInstance)
	{
		_userGrantsInstance = userGrantsInstance ?? throw new ArgumentNullException(nameof(userGrantsInstance));
		_valueMethod = userGrantsInstance.GetType().GetMethod("Value");
	}

	public object InternalInstance => _userGrantsInstance;

	/// <summary>
	///     Gets the user grants.
	/// </summary>
	/// <param name="userId"> The user ID. </param>
	/// <returns> A dictionary of user grants. </returns>
	public async Task<IDictionary<string, object>> Value(string userId)
	{
		var task = (Task<IDictionary<string, object>>)_valueMethod.Invoke(_userGrantsInstance, new object[] { userId });
		return await task.ConfigureAwait(false);
	}

	/// <summary>
	///     Sets the result of the Value method for testing.
	/// </summary>
	/// <param name="userId"> The user ID. </param>
	/// <param name="result"> The result to return. </param>
	public void SetValueResult(string userId, IDictionary<string, object> result)
	{
		var taskCompletionSource = new TaskCompletionSource<IDictionary<string, object>>();
		taskCompletionSource.SetResult(result);

		var dictionaryField = _userGrantsInstance.GetType().GetField("_grants",
			BindingFlags.NonPublic | BindingFlags.Instance);

		if (dictionaryField != null)
		{
			var dictionary = dictionaryField.GetValue(_userGrantsInstance) as Dictionary<string, Task<IDictionary<string, object>>>;
			if (dictionary == null)
			{
				dictionary = new Dictionary<string, Task<IDictionary<string, object>>>();
				dictionaryField.SetValue(_userGrantsInstance, dictionary);
			}

			dictionary[userId] = taskCompletionSource.Task;
		}
	}
}
