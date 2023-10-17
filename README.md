# AirFlowSimulation_Android

## Assets/AR/PointCloud/Scripts/PointCloudGenerator.cs

Unity에서 ARCore를 사용해 실내 공간 3차원 재구성에 사용할 point cloud와 grid 정보를 관리합니다.

- `bool shouldAccumulate()`
  - 카메라가 일정 각도, 거리를 이동 할 때 마다 `accumulatePoints`를 호출하여 point cloud를 생성합니다.

- `void accumulatePoints()`
  - 카메라로부터 받은 RGB와 depth texture를 `PCShaders`로 보내 point cloud를 생성 후 particleBuffer에 그 정보를 담습니다.

- `void checkSideline()`
  - 생성된 point cloud를 사용해 시뮬레이션을 수행할 3차원 grid를 만듭니다.
  - 처음 grid를 초기화 할 때는 지금까지 스캔된 point cloud를 사용해 공간 좌표의 최대, 최소 3차원 좌표를 구해 공간의 길이(gridLength)와 grid cell의 개수(gridSize)를 구합니다.
  - Grid cell의 크기는 10cm로 설정하여 나누었습니다. (AR공간에서 길이 값의 단위는 미터)
  - 각 grid cell 안에 pointThresh이상의 point cloud가 있을 때는 장애물이 있다고 판단해 해당하는 index의 gridArray의 값을 true로 설정해 각 grid cell이 장애물로 채워져 있는 지를 체크합니다.
 
- `void UpdateRawPointCloud()`
  - 카메라로부터 depth정보를 담은 array를 받아옵니다. <br/>그 후 역투영 할 픽셀을 지정하기 위해 픽셀 간격을 설정합니다.
  - 각각의 역투영을 하기 위한 픽셀에 해당하는 depth 값을 이용하여 point cloud의 위치를 vertex로 설정하고 <br/>그 정보를 mesh filter에 넣어 `MeshTopology.Points`로 point cloud를 렌더링합니다.
 
- `void denoiseCheckSideline()`
  - 일정 수의 프레임에서 생성된 point cloud 공간을 만들고 그 공간에서 일정 간격마다 존재하는 point들의 centroid들을 사용합니다.
  - Point cloud 공간의 각 cell안에 pointCount가 일정 수 이상인 point들만을 사용합니다.
  - 위 과정을 통해 noise로 생기는 point cloud의 error를 줄일 수 있습니다.
 
- `void calcMatch(Texture2D prev, Texture2D curr)`
  - 카메라의 drift error를 최소화 하기 위해 각 프레임 간의 feature mathing을 openCV로 구현한calcMatch를 통해 수행합니다.
  - ARCore에서 자체적으로 카메라의 위치를 조정할 때 이전과 현재 프레임 사이의 차이가 없다면 loop가 발생했다고 판단해서 loop fusion을 수행합니다.
 
- `void calcEssen(Texture2D prev, Texture2D curr)`
  - Point cloud를 스캔하면서 주기적으로 프레임 정보를 저장하였고 각 프레임 간의 essential matrix를 사용해 point cloud의 보정을 수행합니다.
  - Essential matrix 계산을 위해 각 프레임 사이에 feature mathcing을 수행합니다.
  - 프레임 사이에 공통되는 feature들을 사용해 essential matrix를 계산합니다.
  - 이후 matrix로 부터 이전 프레임에서부터 이후 프레임으로 향하는 벡터를 얻을 수 있습니다.
 
- `void graphOptimization()`
  - 앞에서 얻은 방향 벡터만큼 이전 프레임에서 생성된 point cloud들을 이동 시켜서 위치 error를 보정합니다.

## Assets/AR/PointCloud/Shaders/PointCloudShader.shader

Unity에서 point cloud 렌더링을 하기 위해 구현한 shader입니다.

Unity에서는 point 렌더링을 할 때 사이즈를 설정할 수 없기 때문에 geometry shader를 사용하기 위해 세팅합니다.

geometry shader를 사용해 각 point들의 위치로부터 추가 정점을 생성해 사각형 오브젝트로 만듭니다. 

이후 fragment shader에서 point의 중심으로부터 일정 거리 이상은 지워지게 하여 원의 형태로 point를 렌더링합니다.

## Assets/AR/PointCloud/Scripts/Communicator.cs

- `void ConnectToServer()`
  - C# .Net Socket을 사용해 TCP 통신 클라이언트를 세팅합니다.
 
- `void startWork()`
  - 서버에 공간 정보를 전송합니다.
  - 통신을 위한 멀티스레드 실행 함수를 호출합니다.
 
- `void work()`, `void send()`
  - `Thread`를 사용해 while 무한 루프를 멀티스레드로 수행해서 서버와 통신을 주고 받게 됩니다.
  - Packet 구조체를 구현해 사용하는 등의 세부적인 동작 방식은 iOS와 동일합니다.

## Assets/AR/PointCloud/Scripts/ArrowAnimation.cs

서버로부터 받은 시뮬레이션 데이터를 사용해 AR환경에서 가시화 하는 코드입니다. <br/>
화살표 기법을 대표 예시로 합니다. 다른 가시화 기법도 각자 script를 가지고 비슷한 형태로 수행됩니다.

- `void startAnimation()`
  - iOS와 동일하게 각 에어컨 기종에 따라 정해진 index의 grid cell에서 화살표 오브젝트를 생성합니다. 
 
- `void createArrows(int i, int j, int k)`
  - 화살표 오브젝트를 생성하고자 하는 index의 grid cell 위치를 이용해 화살표 오브젝트를 생성합니다.
  - 화살표는 생성 될 때 쿼터니온을 사용해 서버로부터 받은 기류의 벡터 방향을 향하게 회전합니다.
 
- `void updateArrows()`
  - `timer` 변수를 사용해 `startAnimation`을 주기적으로 호출하여 화살표를 생성합니다.
  - 각 오브젝트는 주변 8개 cell의 속도 벡터와 온도 값을 trilinear interpolation을 수행하여 자신의 속도 벡터와 온도를 결정합니다.
  - `simulation` 객체를 사용해 각 오브젝트의 속도 벡터와 온도의 interpolation 연산을 수행합니다.
