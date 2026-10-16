#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <openxr/openxr.h>
#include <openxr/openxr_loader_negotiation.h>
#include <atomic>
#include <mutex>
#include <cstdio>
#include <cstring>

#pragma comment(lib, "Ws2_32.lib")
static PFN_xrGetInstanceProcAddr nextGipa = nullptr;
static PFN_xrLocateViews nextLocateViews = nullptr;
static PFN_xrDestroyInstance nextDestroyInstance = nullptr;
static std::mutex mutex;
static SOCKET socketHandle = INVALID_SOCKET;
static sockaddr_in targetAddress = {};
static std::atomic<bool> initialized{false};

static void SendHeadPose(const XrPosef& pose) {
    if (!initialized.load()) return;
    char buffer[256];
    const int count = snprintf(buffer, sizeof(buffer),
        "POSE1|%.7f|%.7f|%.7f|%.8f|%.8f|%.8f|%.8f",
        pose.position.x, pose.position.y, pose.position.z,
        pose.orientation.x, pose.orientation.y, pose.orientation.z, pose.orientation.w);
    if (count > 0 && count < (int)sizeof(buffer)) {
        sendto(socketHandle, buffer, count, 0,
             reinterpret_cast<const sockaddr*>(&targetAddress), sizeof(targetAddress));
    }
}

static void InitSocket() {
    if (initialized.load()) return;
    WSADATA data = {};
    if (WSAStartup(MAKEWORD(2,2), &data) != 0) return;
    socketHandle = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (socketHandle == INVALID_SOCKET) { WSACleanup(); return; }
    targetAddress.sin_family = AF_INET;
    targetAddress.sin_port = htons(39458);
    targetAddress.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    initialized.store(true);
}
static XrResult XRAPI_CALL LayerLocateViews(XrSession session, const XrViewLocateInfo* info,
    XrViewState* state, uint32_t capacity, uint32_t* count, XrView* views) {
    if (!nextLocateViews) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrResult result = nextLocateViews(session, info, state, capacity, count, views);
    if (XR_SUCCEEDED(result) && state && count && *count > 0 && views &&
        (state->viewStateFlags & XR_VIEW_STATE_POSITION_VALID_BIT) &&
        (state->viewStateFlags & XR_VIEW_STATE_ORIENTATION_VALID_BIT)) {
        // View 0 is a valid view pose, but its position contains the eye offset.
        // For a calibrated headset center, midpoint of two stereo eyes is preferable.
        XrPosef pose = views[0].pose;
        if (*count >= 2) {
            pose.position.x = (views[0].pose.position.x + views[1].pose.position.x) * .5f;
            pose.position.y = (views[0].pose.position.y + views[1].pose.position.y) * .5f;
            pose.position.z = (views[0].pose.position.z + views[1].pose.position.z) * .5f;
        }
        SendHeadPose(pose);
    }
    return result;
}
static XrResult XRAPI_CALL LayerDestroyInstance(XrInstance instance) {
    auto f = nextDestroyInstance;
    if (!f) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrResult result = f(instance);
    return result;
}
static XrResult XRAPI_CALL LayerGetInstanceProcAddr(
    XrInstance instance, const char* name, PFN_xrVoidFunction* function) {
    if (!name || !function) return XR_ERROR_VALIDATION_FAILURE;
    if (strcmp(name, "xrLocateViews") == 0) {
        *function = reinterpret_cast<PFN_xrVoidFunction>(LayerLocateViews);
        return XR_SUCCESS;
    }
    if (strcmp(name, "xrDestroyInstance") == 0) {
        *function = reinterpret_cast<PFN_xrVoidFunction>(LayerDestroyInstance);
        return XR_SUCCESS;
    }
    return nextGipa ? nextGipa(instance, name, function) : XR_ERROR_FUNCTION_UNSUPPORTED;
}
static XrResult XRAPI_CALL LayerCreateInstance(
    const XrInstanceCreateInfo* info, const XrApiLayerCreateInfo* layerInfo, XrInstance* instance) {
    if (!layerInfo || !layerInfo->nextInfo || !instance) return XR_ERROR_INITIALIZATION_FAILED;
    const XrApiLayerNextInfo* next = layerInfo->nextInfo;
    nextGipa = next->nextGetInstanceProcAddr;
    XrApiLayerCreateInfo chain = *layerInfo;
    chain.nextInfo = next->next;
    const XrResult result = next->nextCreateApiLayerInstance(info, &chain, instance);
    if (XR_FAILED(result)) return result;
    PFN_xrVoidFunction raw = nullptr;
    if (XR_SUCCEEDED(nextGipa(*instance, "xrLocateViews", &raw))) {
        nextLocateViews = reinterpret_cast<PFN_xrLocateViews>(raw);
    }
    raw = nullptr;
    if (XR_SUCCEEDED(nextGipa(*instance, "xrDestroyInstance", &raw))) {
        nextDestroyInstance = reinterpret_cast<PFN_xrDestroyInstance>(raw);
    }
    InitSocket();
    return result;
}
extern "C" __declspec(dllexport) XrResult XRAPI_CALL xrNegotiateLoaderApiLayerInterface(
    const XrNegotiateLoaderInfo* loaderInfo, const char* layerName,
    XrNegotiateApiLayerRequest* request) {
    if (!loaderInfo || !request || !layerName) return XR_ERROR_INITIALIZATION_FAILED;
    if (strcmp(layerName, "XR_APILAYER_FINGERS_headpose") != 0)
        return XR_ERROR_INITIALIZATION_FAILED;
    if (loaderInfo->minInterfaceVersion > 1 || loaderInfo->maxInterfaceVersion < 1)
        return XR_ERROR_INITIALIZATION_FAILED;
    request->layerInterfaceVersion = 1;
    request->layerApiVersion = XR_CURRENT_API_VERSION;
    request->getInstanceProcAddr = LayerGetInstanceProcAddr;
    request->createApiLayerInstance = LayerCreateInstance;
    return XR_SUCCESS;
}
BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_DETACH) {
        if (socketHandle != INVALID_SOCKET) closesocket(socketHandle);
        if (initialized.load()) WSACleanup();
    }
    return TRUE;
}