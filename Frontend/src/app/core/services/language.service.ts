import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject } from 'rxjs';
import { firstValueFrom } from 'rxjs';
@Injectable({ providedIn: 'root' })
export class LanguageService {

  private readonly STORAGE_KEY = 'acportal_lang';
  availableLanguages = ['fr', 'en'];

  currentLang$ = new BehaviorSubject<string>('fr');

  constructor(private translate: TranslateService) {}

  init(): Promise<void> {
    const saved = localStorage.getItem(this.STORAGE_KEY) || 'fr';
    console.log('langue chargée:', saved);
    this.translate.setDefaultLang('fr');
     return firstValueFrom(this.translate.use(saved)).then(() => {
      this.currentLang$.next(saved);
    });
  }

  switchLanguage(lang: string): void {
    this.translate.use(lang);
    localStorage.setItem(this.STORAGE_KEY, lang);
    this.currentLang$.next(lang);
  }

  getCurrentLang(): string {
    return this.translate.currentLang || 'fr';
  }
}