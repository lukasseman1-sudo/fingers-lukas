cmake_minimum_required(VERSION 3.20)
project(FingersOpenXRLayer LANGUAGES CXX)
include(FetchContent)
FetchContent_Declare(openxr_headers
  GIT_REPOSITORY https://github.com/KhronosGroup/OpenXR-SDK.git
  GIT_TAG release-1.1.49
  GIT_SHALLOW TRUE)
FetchContent_MakeAvailable(openxr_headers)
add_library(FingersOpenXRLayer SHARED FingersOpenXRLayer.cpp)
target_compile_features(FingersOpenXRLayer PRIVATE cxx_std_17)
target_include_directories(FingersOpenXRLayer PRIVATE ${openxr_headers_SOURCE_DIR}/include)
target_link_libraries(FingersOpenXRLayer PRIVATE ws2_32)
set_target_properties(FingersOpenXRLayer PROPERTIES PREFIX "")
