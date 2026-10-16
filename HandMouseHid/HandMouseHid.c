// HandMouse virtual HID mouse. WDK 10/11 + KMDF + VHF required.
// Experimental source: requires signing and hardware testing before installation.
#include <ntddk.h>
#include <wdf.h>
#include <vhf.h>
#include <initguid.h>

DEFINE_GUID(GUID_DEVINTERFACE_HANDMOUSE,0x2a523ec1,0x883c,0x4b0b,0x94,0xc0,0x26,0x90,0x8d,0x1f,0x9c,0x50);
#define FILE_DEVICE_HANDMOUSE 0x8000
#define IOCTL_HANDMOUSE_MOVE CTL_CODE(FILE_DEVICE_HANDMOUSE,0x800,METHOD_BUFFERED,FILE_WRITE_DATA)
typedef struct _HANDMOUSE_INPUT { CHAR X; CHAR Y; UCHAR Buttons; } HANDMOUSE_INPUT;
typedef struct _DEVICE_CONTEXT { VHFHANDLE Vhf; } DEVICE_CONTEXT;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, GetContext)

static const UCHAR ReportDescriptor[] = {
    0x05,0x01, 0x09,0x02, 0xA1,0x01, 0x09,0x01, 0xA1,0x00,
    0x05,0x09, 0x19,0x01, 0x29,0x03, 0x15,0x00, 0x25,0x01,
    0x95,0x03, 0x75,0x01, 0x81,0x02, 0x95,0x01, 0x75,0x05, 0x81,0x03,
    0x05,0x01, 0x09,0x30, 0x09,0x31, 0x15,0x81, 0x25,0x7F,
    0x75,0x08, 0x95,0x02, 0x81,0x06, 0xC0,0xC0
};

EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL OnIoctl;
EVT_WDF_DEVICE_PREPARE_HARDWARE OnPrepare;
EVT_WDF_DEVICE_RELEASE_HARDWARE OnRelease;
EVT_WDF_DRIVER_DEVICE_ADD OnAdd;

VOID OnIoctl(WDFQUEUE Queue,WDFREQUEST Request,size_t OutLen,size_t InLen,ULONG Code)
{
    UNREFERENCED_PARAMETER(OutLen);
    WDFDEVICE dev = WdfIoQueueGetDevice(Queue);
    DEVICE_CONTEXT* ctx = GetContext(dev);
    NTSTATUS st = STATUS_INVALID_DEVICE_REQUEST;
    if (Code == IOCTL_HANDMOUSE_MOVE && InLen == sizeof(HANDMOUSE_INPUT) && ctx->Vhf != NULL) {
        HANDMOUSE_INPUT* input;
        st = WdfRequestRetrieveInputBuffer(Request,sizeof(*input),(PVOID*)&input,NULL);
        if (NT_SUCCESS(st)) {
            UCHAR report[3] = { (UCHAR)(input->Buttons & 7), (UCHAR)input->X, (UCHAR)input->Y };
            HID_XFER_PACKET packet = {0};
            packet.reportBuffer = report;
            packet.reportBufferLen = sizeof(report);
            packet.reportId = 0;
            st = VhfReadReportSubmit(ctx->Vhf,&packet);
        }
    }
    WdfRequestComplete(Request,st);
}

NTSTATUS OnPrepare(WDFDEVICE Device,WDFCMRESLIST Raw,WDFCMRESLIST Translated)
{
    UNREFERENCED_PARAMETER(Raw); UNREFERENCED_PARAMETER(Translated);
    DEVICE_CONTEXT* ctx = GetContext(Device);
    VHF_CONFIG config;
    VHF_CONFIG_INIT(&config,WdfDeviceWdmGetDeviceObject(Device),
        sizeof(ReportDescriptor),(PUCHAR)ReportDescriptor);
    config.VendorID = 0x1209; // for local testing only; not an assigned production identity
    config.ProductID = 0x0001;
    config.VersionNumber = 1;
    config.VhfClientContext = ctx;
    NTSTATUS st = VhfCreate(&config,&ctx->Vhf);
    if (NT_SUCCESS(st)) {
        st = VhfStart(ctx->Vhf);
        if (!NT_SUCCESS(st)) { VhfDelete(ctx->Vhf,TRUE); ctx->Vhf = NULL; }
    }
    return st;
}
NTSTATUS OnRelease(WDFDEVICE Device,WDFCMRESLIST Translated)
{
    UNREFERENCED_PARAMETER(Translated);
    DEVICE_CONTEXT* ctx=GetContext(Device);
    if(ctx->Vhf) {VhfDelete(ctx->Vhf,TRUE);ctx->Vhf=NULL;}
    return STATUS_SUCCESS;
}
NTSTATUS OnAdd(WDFDRIVER Driver,PWDFDEVICE_INIT Init)
{
    UNREFERENCED_PARAMETER(Driver);
    WdfDeviceInitSetDeviceType(Init,FILE_DEVICE_HANDMOUSE);
    WDF_OBJECT_ATTRIBUTES attr; WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attr,DEVICE_CONTEXT);
    WDFDEVICE dev;
    NTSTATUS st=WdfDeviceCreate(&Init,&attr,&dev);
    if(!NT_SUCCESS(st)) return st;
    GetContext(dev)->Vhf=NULL;
    st=WdfDeviceCreateDeviceInterface(dev,&GUID_DEVINTERFACE_HANDMOUSE,NULL);
    if(!NT_SUCCESS(st)) return st;
    WDF_PNPPOWER_EVENT_CALLBACKS pnp; WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnp);
    pnp.EvtDevicePrepareHardware=OnPrepare;
    pnp.EvtDeviceReleaseHardware=OnRelease;
    WdfDeviceSetPnpPowerEventCallbacks(dev,&pnp);
    WDF_IO_QUEUE_CONFIG queue; WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue,WdfIoQueueDispatchSequential);
    queue.EvtIoDeviceControl=OnIoctl;
    return WdfIoQueueCreate(dev,&queue,WDF_NO_OBJECT_ATTRIBUTES,WDF_NO_HANDLE);
}
NTSTATUS DriverEntry(PDRIVER_OBJECT Obj,PUNICODE_STRING Path)
{
    WDF_DRIVER_CONFIG config; WDF_DRIVER_CONFIG_INIT(&config,OnAdd);
    return WdfDriverCreate(Obj,Path,WDF_NO_OBJECT_ATTRIBUTES,&config,WDF_NO_HANDLE);
}
