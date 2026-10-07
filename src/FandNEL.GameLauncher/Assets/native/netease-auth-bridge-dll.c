/*
 * FandNEL NetEase auth bridge for Linux/macOS.
 *
 * The NetEase white-box client authenticates through a JNI native library
 * (com.netease.mc.mod.network.common.Library) that ships as a Windows DLL.
 * On Linux/macOS that DLL cannot load, so Library.AuthenticationAccessToken
 * would fail and the client would be kicked during join.
 *
 * This shared library is dropped into versions/<version>/natives/runtime/
 * (its file name intentionally contains "dll" so the mod's directory scan
 * picks it up and System.load() triggers JNI_OnLoad). It registers a managed
 * implementation of Library.AuthenticationAccessToken that talks to the
 * FandNEL local AuthLibProtocol service:
 *
 *   [int32 len][utf8 gameId] [int32 len][utf8 userId] [int32 len][utf16le serverId]
 *   -> [uint32 result]  (0 = success)
 *
 * Build: gcc -shared -fPIC -o netease-auth-bridge-dll.so bridge.c -I<jni-include>
 */

#include <jni.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <arpa/inet.h>

/* Error codes mirrored from com.netease.mc.mod.authlib.ErrorCode. */
#define ERR_SUCCESS 0
#define ERR_CONNECT_FAILED 2
#define ERR_SEND_DATA 3
#define ERR_AUTH_FAILED 4

static char *ReadSystemProperty(JNIEnv *env, const char *name)
{
    jclass systemClass = (*env)->FindClass(env, "java/lang/System");
    if (systemClass == NULL) return NULL;
    jmethodID getter = (*env)->GetStaticMethodID(env, systemClass, "getProperty",
        "(Ljava/lang/String;)Ljava/lang/String;");
    if (getter == NULL) return NULL;
    jstring key = (*env)->NewStringUTF(env, name);
    if (key == NULL) return NULL;
    jstring value = (jstring)(*env)->CallStaticObjectMethod(env, systemClass, getter, key);
    (*env)->DeleteLocalRef(env, key);
    if (value == NULL) return NULL;
    const char *utf = (*env)->GetStringUTFChars(env, value, NULL);
    char *copy = utf ? strdup(utf) : NULL;
    (*env)->ReleaseStringUTFChars(env, value, utf);
    (*env)->DeleteLocalRef(env, value);
    return copy;
}

static int WriteAll(int fd, const void *buffer, size_t length)
{
    const unsigned char *cursor = buffer;
    while (length > 0)
    {
        ssize_t written = write(fd, cursor, length);
        if (written <= 0)
        {
            if (errno == EINTR) continue;
            return -1;
        }
        cursor += written;
        length -= (size_t)written;
    }
    return 0;
}

static int WriteLengthField(int fd, const void *data, size_t length)
{
    unsigned char header[4];
    unsigned int size = (unsigned int)length;
    header[0] = (unsigned char)(size & 0xff);
    header[1] = (unsigned char)((size >> 8) & 0xff);
    header[2] = (unsigned char)((size >> 16) & 0xff);
    header[3] = (unsigned char)((size >> 24) & 0xff);
    if (WriteAll(fd, header, 4) != 0) return -1;
    return WriteAll(fd, data, length);
}

/* The join serverId is a hex digest, so widening ASCII to UTF-16LE is exact. */
static int WriteUtf16Field(int fd, const char *utf8)
{
    size_t length = strlen(utf8);
    size_t wide = length * 2;
    unsigned char *buffer = malloc(wide ? wide : 1);
    if (buffer == NULL) return -1;
    for (size_t i = 0; i < length; i++)
    {
        buffer[i * 2] = (unsigned char)utf8[i];
        buffer[i * 2 + 1] = 0;
    }
    int result = WriteLengthField(fd, buffer, wide);
    free(buffer);
    return result;
}

static int AuthenticateWithLauncher(int port, const char *gameId, const char *userId, const char *serverId)
{
    int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) return ERR_CONNECT_FAILED;

    struct timeval timeout;
    timeout.tv_sec = 25;
    timeout.tv_usec = 0;
    setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout));
    setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, &timeout, sizeof(timeout));
    int flag = 1;
    setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &flag, sizeof(flag));

    struct sockaddr_in address;
    memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    address.sin_port = htons((unsigned short)port);
    if (inet_pton(AF_INET, "127.0.0.1", &address.sin_addr) != 1)
    {
        close(fd);
        return ERR_CONNECT_FAILED;
    }
    if (connect(fd, (struct sockaddr *)&address, sizeof(address)) != 0)
    {
        close(fd);
        return ERR_CONNECT_FAILED;
    }

    if (WriteLengthField(fd, gameId, strlen(gameId)) != 0 ||
        WriteLengthField(fd, userId, strlen(userId)) != 0 ||
        WriteUtf16Field(fd, serverId) != 0)
    {
        close(fd);
        return ERR_SEND_DATA;
    }

    unsigned char response[4];
    size_t received = 0;
    while (received < 4)
    {
        ssize_t count = read(fd, response + received, 4 - received);
        if (count <= 0)
        {
            close(fd);
            return ERR_AUTH_FAILED;
        }
        received += (size_t)count;
    }
    close(fd);

    unsigned int result = (unsigned int)response[0]
        | ((unsigned int)response[1] << 8)
        | ((unsigned int)response[2] << 16)
        | ((unsigned int)response[3] << 24);
    return result == 0 ? ERR_SUCCESS : (int)result;
}

/* Safe no-op / fallback implementations for the remaining native methods of
 * com.netease.mc.mod.network.common.Library. They are registered so that the
 * client never dies with UnsatisfiedLinkError while running without the
 * Windows authentication DLL. */

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_preInit(JNIEnv *env, jclass clazz) { }
JNIEXPORT jboolean JNICALL Java_com_netease_mc_mod_network_common_Library_init(JNIEnv *env, jclass clazz) { return JNI_TRUE; }
JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_close(JNIEnv *env, jclass clazz) { }
JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_test(JNIEnv *env, jclass clazz) { }
JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_log(JNIEnv *env, jclass clazz, jstring message) { }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_GetToken(
    JNIEnv *env, jclass clazz, jbyteArray buffer, jint length) { }

JNIEXPORT jlong JNICALL Java_com_netease_mc_mod_network_common_Library_NewChaCha(
    JNIEnv *env, jclass clazz, jint mode, jbyteArray key) { return 0; }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_DeleteChaCha(
    JNIEnv *env, jclass clazz, jlong handle) { }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_ChaChaProcess(
    JNIEnv *env, jclass clazz, jlong handle, jbyteArray data, jint length) { }

JNIEXPORT jint JNICALL Java_com_netease_mc_mod_network_common_Library_Skip32(
    JNIEnv *env, jclass clazz, jboolean decode, jbyteArray data, jint length) { return 0; }

/* Encrypted classes in the shipped package are already plain (magic 0xCAFEBABE
 * short-circuits before this call), so echoing the payload back is safe. */
JNIEXPORT jbyteArray JNICALL Java_com_netease_mc_mod_network_common_Library_DecryptClassBytes(
    JNIEnv *env, jclass clazz, jbyteArray input)
{
    return input;
}

/* Content-review stubs (chat/nickname/world review): returning 0 means passed. */
JNIEXPORT jint JNICALL Java_com_netease_mc_mod_network_common_Library_reviewName(
    JNIEnv *env, jclass clazz, jstring text) { return ERR_SUCCESS; }

JNIEXPORT jint JNICALL Java_com_netease_mc_mod_network_common_Library_reviewWord(
    JNIEnv *env, jclass clazz, jstring text) { return ERR_SUCCESS; }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_updateToken(
    JNIEnv *env, jclass clazz, jstring token) { }

/* Obfuscated helpers: behaviour unknown in the original DLL; keep them inert and
 * observe client logs if any gameplay feature misbehaves. */
JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_O0O0O0O0OOOO0(
    JNIEnv *env, jclass clazz, jobject a, jobject b) { }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_OO0OO0O00OO0O(
    JNIEnv *env, jclass clazz, jobject a) { }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_O00O0O0O0O0OO(
    JNIEnv *env, jclass clazz, jobject a, jobject b, jobject c) { }

JNIEXPORT void JNICALL Java_com_netease_mc_mod_network_common_Library_OOO0O0O0O0O0O(
    JNIEnv *env, jclass clazz, jobject a, jobject b) { }

JNIEXPORT jobject JNICALL Java_com_netease_mc_mod_network_common_Library_OO0OO0000OO0O(
    JNIEnv *env, jclass clazz, jobject a) { return NULL; }

JNIEXPORT jint JNICALL
Java_com_netease_mc_mod_network_common_Library_AuthenticationAccessToken(
    JNIEnv *env, jclass clazz, jint port, jstring serverId)
{
    const char *server = serverId ? (*env)->GetStringUTFChars(env, serverId, NULL) : NULL;
    if (server == NULL) return ERR_AUTH_FAILED;

    char *gameId = ReadSystemProperty(env, "launcherGameId");
    char *userId = ReadSystemProperty(env, "userId");
    int code = ERR_AUTH_FAILED;
    if (gameId != NULL && userId != NULL)
        code = AuthenticateWithLauncher((int)port, gameId, userId, server);

    free(gameId);
    free(userId);
    if (serverId) (*env)->ReleaseStringUTFChars(env, serverId, server);
    return code;
}

JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM *vm, void *reserved)
{
    JNIEnv *env = NULL;
    if ((*vm)->GetEnv(vm, (void **)&env, JNI_VERSION_1_8) != JNI_OK)
        return JNI_ERR;

    jclass library = (*env)->FindClass(env, "com/netease/mc/mod/network/common/Library");
    if (library == NULL)
        return JNI_ERR;

    JNINativeMethod methods[] = {
        { "AuthenticationAccessToken", "(ILjava/lang/String;)I",
          (void *)&Java_com_netease_mc_mod_network_common_Library_AuthenticationAccessToken },
        { "preInit", "()V", (void *)&Java_com_netease_mc_mod_network_common_Library_preInit },
        { "init", "()Z", (void *)&Java_com_netease_mc_mod_network_common_Library_init },
        { "close", "()V", (void *)&Java_com_netease_mc_mod_network_common_Library_close },
        { "test", "()V", (void *)&Java_com_netease_mc_mod_network_common_Library_test },
        { "log", "(Ljava/lang/String;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_log },
        { "GetToken", "([BI)V", (void *)&Java_com_netease_mc_mod_network_common_Library_GetToken },
        { "NewChaCha", "(I[B)J", (void *)&Java_com_netease_mc_mod_network_common_Library_NewChaCha },
        { "DeleteChaCha", "(J)V", (void *)&Java_com_netease_mc_mod_network_common_Library_DeleteChaCha },
        { "ChaChaProcess", "(J[BI)V", (void *)&Java_com_netease_mc_mod_network_common_Library_ChaChaProcess },
        { "Skip32", "(Z[BI)I", (void *)&Java_com_netease_mc_mod_network_common_Library_Skip32 },
        { "DecryptClassBytes", "([B)[B", (void *)&Java_com_netease_mc_mod_network_common_Library_DecryptClassBytes },
        { "reviewName", "(Ljava/lang/String;)I", (void *)&Java_com_netease_mc_mod_network_common_Library_reviewName },
        { "reviewWord", "(Ljava/lang/String;)I", (void *)&Java_com_netease_mc_mod_network_common_Library_reviewWord },
        { "updateToken", "(Ljava/lang/String;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_updateToken },
        { "O0O0O0O0OOOO0", "(Ljava/lang/Object;Ljava/lang/Object;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_O0O0O0O0OOOO0 },
        { "OO0OO0O00OO0O", "(Ljava/lang/Object;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_OO0OO0O00OO0O },
        { "O00O0O0O0O0OO", "(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_O00O0O0O0O0OO },
        { "OOO0O0O0O0O0O", "(Ljava/lang/Object;Ljava/lang/Object;)V", (void *)&Java_com_netease_mc_mod_network_common_Library_OOO0O0O0O0O0O },
        { "OO0OO0000OO0O", "(Ljava/lang/Object;)Ljava/lang/Object;", (void *)&Java_com_netease_mc_mod_network_common_Library_OO0OO0000OO0O }
    };
    if ((*env)->RegisterNatives(env, library, methods,
            sizeof(methods) / sizeof(methods[0])) != JNI_OK)
        return JNI_ERR;

    return JNI_VERSION_1_8;
}
