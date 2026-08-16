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
  "OpenAI": {
    "Endpoint": "https://api.deepseek.com",
    "Options": {
      "Endpoint": "https://api.deepseek.com"
    },
    "Model": "deepseek-v4-flash",
    "Credential": {
      "CredentialSource": "ApiKeyCredential",
      "Key": ""
    }
  }
}
```

### 运行
```
cd SharpAgent/SharpAgent && dotnet run
```
