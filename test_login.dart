import 'package:dio/dio.dart';

void main() async {
  final dio = Dio(BaseOptions(
    baseUrl: 'http://127.0.0.1:5071/api',
    validateStatus: (s) => true,
  ));
  
  print("Attempting to login...");
  try {
    final res = await dio.post('/auth/login', data: {
      'email': 'maya17@gmail.com',
      'password': '123' // Or whatever default the user might have used? Actually I don't know the password...
    });
    print(res.statusCode);
    print(res.data);
  } catch (e) {
    print(e);
  }
}
