import 'dart:io';
import 'package:dio/dio.dart';
import 'api_client.dart';

class ImageUploadService {
  static Future<String> uploadImage(File imageFile) async {
    try {
      print('Uploading image to Barter API...');
      final formData = FormData.fromMap({
        'file': await MultipartFile.fromFile(imageFile.path),
      });

      final res = await ApiClient().dio.post('/images/upload', data: formData);
      final imageUrl = res.data['url'] as String;
      
      print('Upload successful: $imageUrl');
      return imageUrl;
    } catch (e) {
      print('Error uploading image to API: $e');
      rethrow;
    }
  }

  static Future<List<String>> uploadMultipleImages(List<File> images) async {
    List<String> urls = [];
    for (int i = 0; i < images.length; i++) {
      print('Uploading image ${i + 1}/${images.length}...');
      try {
        final url = await uploadImage(images[i]);
        urls.add(url);
      } catch (e) {
        print('Failed to upload image $i: $e');
      }
    }
    return urls;
  }
}