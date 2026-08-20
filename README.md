### 安装.NET SDK
```
sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-10.0
```

### 克隆项目
```
git clone https://github.com/bidianqing/SharpAgent
```

### 配置文件
```
{
  "OpenAIClientOptions": {
    "Endpoint": "",
    "Model": "",
    "ApiKey": ""
  }
}
```

### 运行
```
cd SharpAgent/SharpAgent && dotnet run
```
