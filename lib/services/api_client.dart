import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'dart:io';

class ApiClient {
  static final ApiClient _instance = ApiClient._internal();
  factory ApiClient() => _instance;

  final Dio dio;
  final FlutterSecureStorage secureStorage = const FlutterSecureStorage();

  // Use 10.0.2.2 for Android emulator to access localhost, or 127.0.0.1 for iOS simulator.
  // In production, insert your actual deployed URL here.
  static final String baseUrl = Platform.isAndroid ? 'http://10.0.2.2:5071/api' : 'http://127.0.0.1:5071/api';

  ApiClient._internal()
      : dio = Dio(BaseOptions(
          baseUrl: baseUrl,
          connectTimeout: const Duration(seconds: 10),
          receiveTimeout: const Duration(seconds: 10),
          headers: {'Content-Type': 'application/json'},
        )) {
    // Interceptor to inject JWT token
    dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await secureStorage.read(key: 'jwt_token');
        if (token != null && token.isNotEmpty) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
      onError: (DioException e, handler) async {
        if (e.response?.statusCode == 401) {
          // Handle token expiration/logout here if needed
          await secureStorage.delete(key: 'jwt_token');
          await secureStorage.delete(key: 'current_user');
        }
        return handler.next(e);
      },
    ));
  }

  Future<void> saveToken(String token) async {
    await secureStorage.write(key: 'jwt_token', value: token);
  }

  Future<void> clearToken() async {
    await secureStorage.delete(key: 'jwt_token');
  }
}
