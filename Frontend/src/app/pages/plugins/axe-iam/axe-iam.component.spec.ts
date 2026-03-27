import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AxeIamComponent } from './axe-iam.component';

describe('AxeIamComponent', () => {
  let component: AxeIamComponent;
  let fixture: ComponentFixture<AxeIamComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AxeIamComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AxeIamComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
