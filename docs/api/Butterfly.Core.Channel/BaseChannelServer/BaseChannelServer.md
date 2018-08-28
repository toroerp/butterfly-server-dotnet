# BaseChannelServer constructor

```csharp
public BaseChannelServer(int mustReceiveHeartbeatMillis = 5000, 
    Func<string, string, object> getAuthToken = null, 
    Func<string, string, Task<object>> getAuthTokenAsync = null, Func<object, string> getId = null, 
    Func<object, Task<string>> getIdAsync = null)
```

## See Also

* class [BaseChannelServer](../BaseChannelServer.md)
* namespace [Butterfly.Core.Channel](../../Butterfly.Core.md)

<!-- DO NOT EDIT: generated by xmldocmd for Butterfly.Core.dll -->