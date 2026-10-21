# MWORKS数据接收脚本使用说明

## 功能说明
本脚本用于接收MWORKS仿真软件发送的数据，并将其显示在Unity游戏界面上。

## 配置参数
- **localAddress**: 本地IP地址，默认为127.0.0.1
- **remoteAddress**: 远程IP地址，默认为127.0.0.1
- **localPort**: 本地端口，默认为32701
- **deltaT**: 时间步长，默认为0.005

## 使用方法

### 方法一：自动初始化（推荐）
1. 在Unity编辑器中打开SampleScene场景
2. 创建一个空的GameObject，命名为"Initializer"
3. 选择该GameObject，在Inspector面板中点击"Add Component"
4. 搜索并添加"InitializeMWORKSReceiver"组件
5. 运行场景，脚本会自动创建MWORKSDataReceiver实例并开始接收数据

### 方法二：手动添加
1. 在Unity编辑器中打开SampleScene场景
2. 创建一个空的GameObject，命名为"MWORKSDataReceiver"
3. 选择该GameObject，在Inspector面板中点击"Add Component"
4. 搜索并添加"MWORKSDataReceiver"组件
5. 根据需要修改Inspector面板中的配置参数
6. 运行场景，开始接收数据

## 数据显示
- 运行场景后，接收到的数据会显示在游戏界面的左上角
- 数据以文本形式显示，支持自动换行
- 数据更新时会实时刷新显示

## 注意事项
1. 确保MWORKS仿真软件的发送配置与本脚本的接收配置匹配
2. 确保本地端口32701未被其他程序占用
3. 如需修改端口号，请同时修改MWORKS和本脚本中的配置
4. 运行场景时，Unity编辑器的Console窗口会显示初始化信息

## 故障排查
- 如果没有接收到数据，请检查：
  1. MWORKS是否正在发送数据
  2. 网络连接是否正常
  3. 配置参数是否正确
  4. 防火墙是否阻止了UDP数据传输

- 如遇到其他问题，请查看Unity编辑器的Console窗口中的错误信息