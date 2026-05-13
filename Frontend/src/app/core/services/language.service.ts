import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class LanguageService {

  private readonly STORAGE_KEY = 'acportal_lang';
  availableLanguages = ['fr', 'en'];

  constructor(private translate: TranslateService) {}

  init(): void {
    const saved = localStorage.getItem(this.STORAGE_KEY) || 'fr';
    this.translate.setDefaultLang('fr');
    this.translate.use(saved);
  }

  switchLanguage(lang: string): void {
    this.translate.use(lang);
    localStorage.setItem(this.STORAGE_KEY, lang);
  }

  getCurrentLang(): string {
    return this.translate.currentLang || 'fr';
  }
}