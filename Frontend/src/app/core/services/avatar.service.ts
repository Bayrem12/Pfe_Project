import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

/**
 * Lightweight per-user avatar store.
 * Avatars are persisted as base64 data URLs in localStorage so the picture
 * survives reloads without requiring a backend column / migration.
 *
 * Any uploaded image is automatically resized & re-encoded to a reasonable
 * size (max edge 1024px, JPEG q=0.9) so the user can pick any photo without
 * worrying about size limits — like Instagram does.
 */
@Injectable({ providedIn: 'root' })
export class AvatarService {
  /** Maximum edge (px) we keep after auto-resizing. */
  static readonly MAX_EDGE_PX = 1024;

  /** Allowed MIME types for upload. */
  static readonly ALLOWED_TYPES = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp', 'image/gif', 'image/bmp', 'image/heic', 'image/heif'];

  private readonly subject = new BehaviorSubject<string | null>(null);
  readonly avatar$: Observable<string | null> = this.subject.asObservable();

  private currentUserId: string | null = null;

  /** Switch the active user; loads (or clears) the avatar for that user. */
  setUser(userId: string | null): void {
    this.currentUserId = userId;
    this.subject.next(this.read(userId));
  }

  /** Returns the current data URL synchronously, if any. */
  get current(): string | null {
    return this.subject.value;
  }

  /** Persist a new avatar data URL for the current user. */
  setAvatar(dataUrl: string): void {
    if (!this.currentUserId) return;
    try {
      localStorage.setItem(this.keyFor(this.currentUserId), dataUrl);
      this.subject.next(dataUrl);
    } catch {
      // Quota exceeded or unavailable storage – fall back to in-memory only.
      this.subject.next(dataUrl);
    }
  }

  /** Remove the avatar for the current user. */
  clearAvatar(): void {
    if (!this.currentUserId) return;
    localStorage.removeItem(this.keyFor(this.currentUserId));
    this.subject.next(null);
  }

  /**
   * Read an image file and return a resized, compressed base64 data URL.
   * No hard size limit — large images are downscaled automatically so the user
   * can pick any photo from their device.
   */
  readFile(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      // Accept any image/* type even if not in our list (browsers report odd types for HEIC etc.)
      if (!file.type.startsWith('image/')) {
        reject({ key: 'profile.avatar.errorType' });
        return;
      }
      const reader = new FileReader();
      reader.onerror = () => reject({ key: 'profile.avatar.errorRead' });
      reader.onload = () => {
        const rawDataUrl = String(reader.result || '');
        if (!rawDataUrl) {
          reject({ key: 'profile.avatar.errorRead' });
          return;
        }
        // GIFs would lose animation if re-encoded — keep as-is.
        if (file.type === 'image/gif') {
          resolve(rawDataUrl);
          return;
        }
        this.resizeDataUrl(rawDataUrl)
          .then(resolve)
          .catch(() => resolve(rawDataUrl)); // fall back to raw if resize fails
      };
      reader.readAsDataURL(file);
    });
  }

  /** Downscale (longest edge → MAX_EDGE_PX) and re-encode to JPEG. */
  private resizeDataUrl(dataUrl: string): Promise<string> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onerror = () => reject(new Error('decode failed'));
      img.onload = () => {
        const max = AvatarService.MAX_EDGE_PX;
        let { width, height } = img;
        if (width <= max && height <= max) {
          // Still recompress to JPEG to keep storage small.
        } else if (width >= height) {
          height = Math.round((height * max) / width);
          width = max;
        } else {
          width = Math.round((width * max) / height);
          height = max;
        }
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          reject(new Error('no 2d context'));
          return;
        }
        ctx.drawImage(img, 0, 0, width, height);
        try {
          resolve(canvas.toDataURL('image/jpeg', 0.9));
        } catch (e) {
          reject(e);
        }
      };
      img.src = dataUrl;
    });
  }

  private read(userId: string | null): string | null {
    if (!userId) return null;
    return localStorage.getItem(this.keyFor(userId));
  }

  private keyFor(userId: string): string {
    return `profile_avatar_${userId}`;
  }
}
